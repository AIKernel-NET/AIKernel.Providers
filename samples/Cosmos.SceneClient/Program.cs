using System.Buffers.Binary;
using System.IO.Compression;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using AIKernel.Abstractions.Capabilities;
using AIKernel.Dtos.Capabilities;
using AIKernel.Providers.Cosmos;

if (args.Length == 0)
{
    Console.WriteLine("--offline-demo | --nim <host-config.json> <approved-public.png>");
    Console.WriteLine("The --nim mode sends the selected image to the explicitly configured managed endpoint. It does not start, stop or place models.");
    return;
}
bool offline = args is ["--offline-demo"];
if (!offline && !(args.Length == 3 && args[0] == "--nim")) throw new ArgumentException("Invalid arguments.");
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{ UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true };
HostConfig? config = offline ? null : JsonSerializer.Deserialize<HostConfig>(await File.ReadAllTextAsync(args[1]), jsonOptions) ?? throw new ArgumentException("Invalid host config.");
var directory = offline ? Path.Combine(Path.GetTempPath(), "cosmos-scene-demo-" + Guid.NewGuid().ToString("N")) : config!.JournalDirectory;
ISceneReasonerBackend backend = offline ? new OfflineBackend() : new CosmosNimBackend(new()
{
    Endpoint = new(config!.Endpoint), EndpointId = config.EndpointId, Model = config.Model, Revision = config.Revision,
    Precision = config.Precision, Runtime = config.Runtime, ImageDigest = config.ImageDigest, GpuIds = config.GpuIds,
    AllowedAddresses = config.AllowedAddresses.Select(IPAddress.Parse).ToArray(),
    BearerToken = Environment.GetEnvironmentVariable("AIKERNEL_COSMOS_TOKEN"),
    InferenceTransportTimeout = TimeSpan.FromSeconds(config.TransportTimeoutSeconds)
}, new SceneDeploymentFileSource(config.DeploymentEvidenceFile));
try
{
    await using ISceneService service = new SceneService(new() { Enabled = true, JournalDirectory = directory }, backend, new PublicSceneAuthorizer());
    var readiness = await service.ProbeAsync();
    Console.WriteLine($"backend={readiness.Provider.Backend}; mock={readiness.Provider.IsMock}; readiness={readiness.State}; reason={readiness.ReasonCode}");
    if (readiness.State != "ready") return;
    // Replace this principal with the host's authenticated principal. Never read claims from request_json.
    var caller = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "public-demo-caller")], "local-demo"));
    var session = service.OpenSession(caller);
    ICapabilityModuleInvoker invoker = session; // Official AIKernel contract; no local copy.
    var pixels = offline ? DemoPng() : await File.ReadAllBytesAsync(args[2]); // Local capture adapter, not a model-accessible path.
    if (pixels.Length < 24 || pixels.Length > 4 * 1024 * 1024) throw new ArgumentException("Bounded PNG required.");
    int width = BinaryPrimitives.ReadInt32BigEndian(pixels.AsSpan(16, 4));
    int height = BinaryPrimitives.ReadInt32BigEndian(pixels.AsSpan(20, 4));
    var epoch = "epoch-" + Guid.NewGuid().ToString("N");
    await Analyze("req-" + Guid.NewGuid().ToString("N"), cancel: false, stale: false);
    if (offline)
    {
        await Analyze("req-" + Guid.NewGuid().ToString("N"), cancel: true, stale: false);
        await Analyze("req-" + Guid.NewGuid().ToString("N"), cancel: false, stale: true);
    }
    Console.WriteLine("Analysis only. No proposal has been adopted or executed.");

    async Task Analyze(string id, bool cancel, bool stale)
    {
        var capture = DateTimeOffset.UtcNow.AddSeconds(stale ? -10 : 0);
        var snapshot = new SceneSnapshot(epoch, "snap-" + id, "program-composite", capture, 41, 8, "camera-wide");
        var receipt = await session.RegisterMediaAsync(new(snapshot, "frame-1", 0, "image/png", width, height,
            SceneProtocol.ComputeMediaHash(pixels), DateTimeOffset.UtcNow.AddMinutes(1)), new MemoryStream(pixels));
        var request = new SceneRequest("scene-observation.v1", id, epoch, snapshot.SnapshotId, snapshot.CaptureSource, capture,
            5000, 3000, 41, 8, snapshot.CameraId, "public-policy-3", "public", "shadow",
            [new(receipt.MediaId, "frame-1", "image/png", width, height, 0)], [new("actor-a", 12, "listening", true)],
            [new("board-a", "presentation-surface")], [new("look_at", ["actor-a"], ["board-a"])], 2);
        string json = SceneProtocol.SerializeRequest(request); // Retain these EXACT bytes for retries.
        var reply = await invoker.InvokeAsync(new("inv-" + Guid.NewGuid().ToString("N"), SceneService.CapabilityId, "scene.analyze",
            new Dictionary<string, string> { ["request_json"] = json }, SceneProtocol.ComputeInputHash(json), null, new Dictionary<string, string>()));
        if (!reply.Succeeded) { Console.WriteLine($"API rejected: {reply.ErrorCode}"); return; }
        if (cancel) reply = await Query("scene.cancel");
        var result = SceneProtocol.DeserializeResult(reply.Metadata[SceneService.ResultMetadataKey]);
        while (result.Status is "accepted" or "queued" or "running")
        {
            await Task.Delay(100);
            reply = await Query("scene.status");
            if (!reply.Succeeded) throw new InvalidOperationException(reply.ErrorCode);
            result = SceneProtocol.DeserializeResult(reply.Metadata[SceneService.ResultMetadataKey]);
        }
        Console.WriteLine($"status={result.Status}; mode={result.Mode}; analysisOnly={result.AnalysisOnly}; freshness={result.FreshnessVerified}; proposals={result.Proposals.Count}; reason={result.ErrorCode ?? "none"}");
        // TheoriaCast must revalidate current epoch/revisions/deadline, manual precedence and proposalId before adopting any propose-mode candidate.
        // shadow and observe-only are never executed. Succeeded is API processing, not job completion or performed.
        ValueTask<CapabilityInvocationResult> Query(string operation) => invoker.InvokeAsync(new("inv-" + Guid.NewGuid().ToString("N"), SceneService.CapabilityId,
            operation, new Dictionary<string, string> { ["requestId"] = id }, null, null, new Dictionary<string, string>()));
    }
}
finally { (backend as IDisposable)?.Dispose(); }

static byte[] DemoPng()
{
    using var file = new MemoryStream(); file.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    var header = new byte[13]; BinaryPrimitives.WriteInt32BigEndian(header, 2); BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), 2); header[8] = 8; header[9] = 6;
    Chunk("IHDR", header);
    using var compressed = new MemoryStream();
    using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, true)) zlib.Write(new byte[18]);
    Chunk("IDAT", compressed.ToArray()); Chunk("IEND", []); return file.ToArray();
    void Chunk(string type, byte[] data)
    {
        var word = new byte[4]; BinaryPrimitives.WriteInt32BigEndian(word, data.Length); file.Write(word);
        var payload = System.Text.Encoding.ASCII.GetBytes(type).Concat(data).ToArray(); file.Write(payload); uint crc = uint.MaxValue;
        foreach (byte value in payload) { crc ^= value; for (int i = 0; i < 8; i++) crc = (crc >> 1) ^ (0xedb88320u & (uint)-(int)(crc & 1)); }
        BinaryPrimitives.WriteUInt32BigEndian(word, ~crc); file.Write(word);
    }
}
sealed class PublicSceneAuthorizer : ISceneAuthorizer
{
    public SceneGrant Authorize(ClaimsPrincipal caller)
    {
        if (caller.Identity?.IsAuthenticated != true || caller.FindFirst(ClaimTypes.NameIdentifier)?.Value != "public-demo-caller") throw new SceneException("permission_denied");
        return new("public-demo-caller", "public", "public-policy-3", new Dictionary<string, long> { ["actor-a"] = 12 },
            new HashSet<string> { "look_at" }, new HashSet<string> { "board-a" },
            new(TimeSpan.Zero, TimeSpan.Zero, DateTimeOffset.UtcNow.AddMinutes(1))); // Same-process capture only; remote clocks require real calibration.
    }
}
sealed class OfflineBackend : ISceneReasonerBackend
{
    public SceneBackendIdentity Identity { get; } = new("offline-demo", "none", "none", "none", "sample", "none", [], true);
    public ValueTask<SceneReadiness> ProbeAsync(CancellationToken token) => ValueTask.FromResult(new SceneReadiness("ready", "ready", true, Identity));
    public async Task<SceneBackendReply> AnalyzeAsync(SceneBackendInput input, CancellationToken token)
    { await Task.Delay(200, token); return new("{\"observations\":[],\"proposals\":[]}", Identity); }
    public ValueTask<string> RequestCancellationAsync(string id, CancellationToken token) => ValueTask.FromResult("unsupported");
}
sealed record HostConfig(string Endpoint, string EndpointId, string Model, string Revision, string Precision, string Runtime,
    string ImageDigest, string[] GpuIds, string[] AllowedAddresses, string JournalDirectory, string DeploymentEvidenceFile, int TransportTimeoutSeconds);
