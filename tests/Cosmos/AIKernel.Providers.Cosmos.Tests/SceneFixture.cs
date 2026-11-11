using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Claims;
using System.Threading.Channels;
using AIKernel.Dtos.Capabilities;

namespace AIKernel.Providers.Cosmos.Tests;

internal sealed class ManualClock : TimeProvider
{
    private long _milliseconds;
    internal long WallOffsetMilliseconds;
    private readonly List<ClockTimer> _timers = [];
    public override long TimestampFrequency => 1000;
    public override long GetTimestamp() => Interlocked.Read(ref _milliseconds);
    public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 11, 11, 3, 0, 0, TimeSpan.Zero).AddMilliseconds(GetTimestamp() + WallOffsetMilliseconds);
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ClockTimer(this, callback, state, GetTimestamp() + (long)dueTime.TotalMilliseconds);
        lock (_timers) _timers.Add(timer);
        return timer;
    }
    internal void Advance(int milliseconds)
    {
        Interlocked.Add(ref _milliseconds, milliseconds);
        ClockTimer[] ready;
        lock (_timers) ready = _timers.Where(t => !t.Disposed && t.Due <= GetTimestamp()).ToArray();
        foreach (var timer in ready) { timer.Disposed = true; timer.Callback(timer.State); }
    }
    private sealed class ClockTimer(ManualClock clock, TimerCallback callback, object? state, long due) : ITimer
    {
        internal TimerCallback Callback = callback;
        internal object? State = state;
        internal long Due = due;
        internal bool Disposed;
        public bool Change(TimeSpan dueTime, TimeSpan period) { Due = clock.GetTimestamp() + (long)dueTime.TotalMilliseconds; return !Disposed; }
        public void Dispose() => Disposed = true;
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}

internal sealed class FakeBackend : ISceneReasonerBackend
{
    internal readonly Channel<SceneBackendInput> Calls = Channel.CreateUnbounded<SceneBackendInput>();
    private readonly List<(SceneBackendInput Input, TaskCompletionSource<SceneBackendReply> Reply)> _pending = [];
    internal bool Block;
    internal int Count;
    internal int CancelCount;
    internal string CancelReceipt = "unsupported";
    internal string State = "ready";
    internal string? Output;
    internal SceneException? Failure;
    internal Func<Task>? BeforeProbe;
    public SceneBackendIdentity Identity { get; internal set; } = new("test-double", "test-model", "test-revision", "fp32", "offline", "test-endpoint", ["test-gpu"], true);
    public async ValueTask<SceneReadiness> ProbeAsync(CancellationToken cancellationToken)
    {
        if (BeforeProbe is not null) await BeforeProbe();
        return new(State, State == "ready" ? "ready" : "backend_" + State, State == "ready", Identity);
    }
    public async Task<SceneBackendReply> AnalyzeAsync(SceneBackendInput input, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref Count);
        var completion = new TaskCompletionSource<SceneBackendReply>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_pending) _pending.Add((input, completion));
        Calls.Writer.TryWrite(input);
        if (Failure is not null) completion.TrySetException(Failure);
        else if (!Block) completion.TrySetResult(Reply(input));
        return await completion.Task;
    }
    internal SceneBackendReply Reply(SceneBackendInput input) => new(Output ?? SceneJson.Write(new SceneModelResult(
        [new("obs-1", "actor-a", "board-a", "attention_target_mismatch", false, [input.Request.Media[0].FrameId])],
        input.Request.MaxProposals == 0 || input.Request.Mode == "observe-only" ? [] : [new("actor-a", 12, "look_at", "board-a", ["obs-1"])])), Identity);
    internal void ReleaseAll()
    {
        lock (_pending) foreach (var pending in _pending) pending.Reply.TrySetResult(Reply(pending.Input));
    }
    internal void FailAll(SceneException error)
    {
        lock (_pending) foreach (var pending in _pending) pending.Reply.TrySetException(error);
    }
    public ValueTask<string> RequestCancellationAsync(string requestId, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref CancelCount);
        if (CancelReceipt == "confirmed") FailAll(new SceneException("backend_canceled"));
        return ValueTask.FromResult(CancelReceipt);
    }
}

internal sealed class SceneFixture : IAsyncDisposable, ISceneAuthorizer
{
    internal readonly ManualClock Clock = new();
    internal readonly FakeBackend Backend = new();
    internal readonly string Directory = Path.Combine(Path.GetTempPath(), "aikernel-cosmos-test-" + Guid.NewGuid().ToString("N"));
    internal SceneService Service;
    internal SceneSession Session;
    internal bool ClockKnown = true;
    internal string Policy = "public-policy-3";
    internal long ActorRevision = 12;
    internal Func<ClaimsPrincipal, SceneGrant, SceneGrant>? Authorization;
    internal SceneOptions Options;
    internal ClaimsPrincipal Principal(string id = "caller-a") => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id)], "test-auth"));
    internal SceneFixture(bool enabled = true, int maxJobs = 1000)
    {
        Options = new() { Enabled = enabled, JournalDirectory = Directory, MaxJobs = maxJobs };
        Service = new(Options, Backend, this, Clock);
        Session = Service.OpenSession(Principal());
    }
    public SceneGrant Authorize(ClaimsPrincipal authenticatedCaller)
    {
        var grant = new SceneGrant(
        authenticatedCaller.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new SceneException("permission_denied"),
        "public", Policy, new Dictionary<string, long> { ["actor-a"] = ActorRevision, ["actor-b"] = 7 },
        new HashSet<string> { "look_at", "move_to" }, new HashSet<string> { "board-a", "spot-a" },
        ClockKnown ? new(TimeSpan.Zero, TimeSpan.FromMilliseconds(1), Clock.GetUtcNow().AddHours(1)) : null);
        return Authorization is null ? grant : Authorization(authenticatedCaller, grant);
    }

    internal async Task<SceneRequest> Request(string id = "req-1", SceneSession? session = null, int offset = 0)
    {
        var capture = Clock.GetUtcNow().AddMilliseconds(offset);
        var snapshot = new SceneSnapshot("epoch-1", "snap-" + id, "program-composite", capture, 41, 8, "camera-wide");
        var png = Png();
        var media = await (session ?? Session).RegisterMediaAsync(new(snapshot, "frame-1", 0, "image/png", 2, 2,
            SceneJson.Hash(png), Clock.GetUtcNow().AddMinutes(1)), new MemoryStream(png));
        return new("scene-observation.v1", id, snapshot.SessionEpoch, snapshot.SnapshotId, snapshot.CaptureSource,
            capture, 5000, 3000, 41, 8, snapshot.CameraId, Policy, "public", "shadow",
            [new(media.MediaId, "frame-1", "image/png", 2, 2, 0)], [new("actor-a", 12, "listening", true)],
            [new("board-a", "presentation-surface")], [new("look_at", ["actor-a"], ["board-a"])], 2);
    }
    internal Task<CapabilityInvocationResult> Submit(SceneRequest request, SceneSession? session = null)
        => Invoke("scene.analyze", new Dictionary<string, string> { ["request_json"] = SceneJson.Write(request) }, session);
    internal async Task<CapabilityInvocationResult> Invoke(string operation, IReadOnlyDictionary<string, string>? arguments = null, SceneSession? session = null)
        => await (session ?? Session).InvokeAsync(new("inv-" + Guid.NewGuid().ToString("N"), SceneService.CapabilityId, operation,
            arguments ?? new Dictionary<string, string>(), null, "existing-replay-hash", new Dictionary<string, string>()));
    internal async Task<SceneAnalysisResult> Status(string id = "req-1", SceneSession? session = null)
    {
        var result = await Invoke("scene.status", new Dictionary<string, string> { ["requestId"] = id }, session);
        Assert.True(result.Succeeded, result.ErrorCode);
        return Parse(result);
    }
    internal static SceneAnalysisResult Parse(CapabilityInvocationResult result)
        => SceneJson.Read<SceneAnalysisResult>(result.Metadata[SceneService.ResultMetadataKey], 128 * 1024);
    internal async Task<SceneAnalysisResult> Completed(string id = "req-1")
    {
        SceneAnalysisResult? result = null;
        await Eventually(async () => { result = await Status(id); return result.Status is not ("queued" or "running"); });
        return result!;
    }
    internal async Task WaitForCall() => await Backend.Calls.Reader.ReadAsync(TestContext.Current.CancellationToken)
        .AsTask().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    internal static async Task Eventually(Func<Task<bool>> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!await condition()) await Task.Delay(10, timeout.Token);
    }
    internal async Task Restart()
    {
        Backend.Block = false; Backend.ReleaseAll();
        await Service.DisposeAsync();
        Service = new(Options, Backend, this, Clock); Session = Service.OpenSession(Principal());
    }
    public async ValueTask DisposeAsync()
    {
        Backend.Block = false; Backend.ReleaseAll();
        await Service.DisposeAsync();
        // Only a freshly generated, resolved test-owned directory is removed.
        var root = Path.GetFullPath(Directory);
        if (Path.GetDirectoryName(root) == Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)
            && Path.GetFileName(root).StartsWith("aikernel-cosmos-test-", StringComparison.Ordinal))
            System.IO.Directory.Delete(root, recursive: true);
    }
    internal static byte[] Png(int width = 2, int height = 2)
    {
        using var png = new MemoryStream();
        png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width); BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; header[9] = 6;
        Chunk(png, "IHDR", header);
        using var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true)) z.Write(new byte[height * (width * 4 + 1)]);
        Chunk(png, "IDAT", compressed.ToArray()); Chunk(png, "IEND", []);
        return png.ToArray();
    }
    private static void Chunk(Stream stream, string type, byte[] data)
    {
        byte[] length = new byte[4]; BinaryPrimitives.WriteInt32BigEndian(length, data.Length); stream.Write(length);
        var chunk = System.Text.Encoding.ASCII.GetBytes(type).Concat(data).ToArray(); stream.Write(chunk);
        uint crc = uint.MaxValue;
        foreach (var value in chunk) { crc ^= value; for (int i = 0; i < 8; i++) crc = (crc >> 1) ^ (0xedb88320u & (uint)-(int)(crc & 1)); }
        BinaryPrimitives.WriteUInt32BigEndian(length, ~crc); stream.Write(length);
    }
}
