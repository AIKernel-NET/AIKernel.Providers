namespace AIKernel.Providers.Cosmos.Tests;

public sealed class SceneReadinessClockErrorTests
{
    private const string Secret = "private-clock-token/internal/path";

    [Theory]
    [InlineData("backend", "generic")]
    [InlineData("backend", "custom")]
    [InlineData("backend", "known")]
    [InlineData("backend", "cancel")]
    [InlineData("backend", "task")]
    [InlineData("identity", "generic")]
    [InlineData("identity", "custom")]
    [InlineData("identity", "known")]
    [InlineData("identity", "cancel")]
    [InlineData("identity", "task")]
    public async Task ErrorPathClockFailureCannotExposeDetailsOrRetainReady(string source, string kind)
    {
        await using var f = new SceneFixture(); var (clock, backend) = await Wrap(f);
        Assert.True((await f.Service.ProbeAsync(TestContext.Current.CancellationToken)).DeploymentVerified);
        var before = Snapshot(f);
        using var internalToken = new CancellationTokenSource(); internalToken.Cancel();
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        if (source == "identity") backend.FailIdentity = true;
        else backend.Probe = _ => ValueTask.FromException<SceneReadiness>(new IOException(Secret));
        clock.Reads = 0; clock.Read = _ => throw Fault(kind, internalToken.Token);
        try
        {
            var probe = f.Service.ProbeAsync(caller.Token).AsTask();
            if (kind is "cancel" or "task") await AssertCancellation(probe, kind, caller.Token);
            else AssertFailed(await probe);
            Assert.Equal(1, clock.Reads);
        }
        finally { clock.Read = null; backend.FailIdentity = false; backend.Probe = null; }
        AssertClosedCache((await Catalog(f)).Readiness);
        Assert.Equal(before, Snapshot(f)); Assert.Equal(0, f.Backend.Count);
        Assert.Equal("ready", (await f.Service.ProbeAsync(TestContext.Current.CancellationToken)).State);
    }

    [Theory]
    [InlineData("generic")]
    [InlineData("custom")]
    [InlineData("cancel")]
    [InlineData("task")]
    public async Task MainClockFailurePreservesCancellationAndNeverPublishesNewReady(string kind)
    {
        await using var f = new SceneFixture(); var (clock, _) = await Wrap(f);
        var previous = await f.Service.ProbeAsync(TestContext.Current.CancellationToken); var before = Snapshot(f);
        using var internalToken = new CancellationTokenSource(); internalToken.Cancel();
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        clock.Reads = 0; clock.Read = _ => throw Fault(kind, internalToken.Token);
        try
        {
            var probe = f.Service.ProbeAsync(caller.Token).AsTask();
            if (kind is "cancel" or "task") await AssertCancellation(probe, kind, caller.Token);
            else AssertFailed(await probe);
            Assert.Equal(kind is "cancel" or "task" ? 1 : 2, clock.Reads);
        }
        finally { clock.Read = null; }
        var cached = (await Catalog(f)).Readiness;
        if (kind is "cancel" or "task") Assert.Equal(SceneJson.Write(previous), SceneJson.Write(cached));
        else AssertClosedCache(cached);
        Assert.Equal(before, Snapshot(f)); Assert.Equal(0, f.Backend.Count);
    }

    [Fact]
    public async Task IntermittentClockFailureKeepsFreshProbeFailedResult()
    {
        await using var f = new SceneFixture(); var (clock, _) = await Wrap(f);
        clock.Reads = 0; clock.Read = call => call == 1 ? throw new IOException(Secret) : f.Clock.GetUtcNow();
        try
        {
            var result = await f.Service.ProbeAsync(TestContext.Current.CancellationToken);
            AssertFailed(result); Assert.Equal(2, clock.Reads);
            Assert.Equal(SceneJson.Write(result), SceneJson.Write((await Catalog(f)).Readiness));
        }
        finally { clock.Read = null; }
        Assert.Equal(0, f.Backend.Count);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("task")]
    public async Task SecondaryClockCancellationUsesOnlyTheCallerToken(string kind)
    {
        await using var f = new SceneFixture(); var (clock, _) = await Wrap(f);
        using var internalToken = new CancellationTokenSource(); internalToken.Cancel();
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        clock.Reads = 0; clock.Read = call => throw (call == 1 ? new IOException(Secret) : Fault(kind, internalToken.Token));
        try
        {
            await AssertCancellation(f.Service.ProbeAsync(caller.Token).AsTask(), kind, caller.Token);
            Assert.Equal(2, clock.Reads);
        }
        finally { clock.Read = null; }
        AssertClosedCache((await Catalog(f)).Readiness); Assert.Equal(0, f.Backend.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ErrorClockReentrantStopCannotReviveOrTouchSuccessor(bool throwAfterStop)
    {
        await using var f = new SceneFixture(); var (clock, backend) = await Wrap(f);
        backend.Probe = _ => ValueTask.FromException<SceneReadiness>(new IOException(Secret));
        var old = f.Service; string? before = null;
        clock.Reads = 0;
        clock.Read = _ =>
        {
            clock.Read = null;
            old.DisposeAsync().AsTask().GetAwaiter().GetResult();
            f.Service = new(f.Options, f.Backend, f, f.Clock); f.Session = f.Service.OpenSession(f.Principal());
            before = Snapshot(f);
            return throwAfterStop ? throw new IOException(Secret) : f.Clock.GetUtcNow();
        };
        try
        {
            var result = await old.ProbeAsync(TestContext.Current.CancellationToken);
            Assert.Equal("disabled", result.State); Assert.Equal("disabled", result.ReasonCode);
            Assert.False(result.DeploymentVerified); Assert.Null(result.RetryAfterMs);
            Assert.Equal(1, clock.Reads); Assert.NotNull(before); Assert.Equal(before, Snapshot(f));
            Assert.Equal("journal_unavailable", Assert.Throws<SceneException>(() => new SceneService(f.Options, f.Backend, f, f.Clock)).Code);
        }
        finally { clock.Read = null; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateProbeFailurePreservesQuarantineWithoutCallingClock(bool clockThrows)
    {
        await using var f = new SceneFixture(); var (clock, backend) = await Wrap(f);
        f.Backend.Block = true; await f.Submit(await f.Request()); await f.WaitForCall();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        backend.Probe = async _ => { entered.TrySetResult(); await release.Task; throw new IOException(Secret); };
        var pending = f.Service.ProbeAsync(TestContext.Current.CancellationToken).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        try
        {
            f.Backend.FailAll(new("backend_transport_unconfirmed", true));
            await SceneFixture.Eventually(async () => (await Catalog(f)).ActiveInference == 0);
            await SceneFixture.Eventually(async () => (await f.Status()).BackendCancellation != "requested");
            var before = Snapshot(f); clock.Reads = 0;
            clock.Read = _ => clockThrows ? throw new IOException(Secret) : f.Clock.GetUtcNow();
            release.TrySetResult(); var result = await pending;
            Assert.Equal("degraded", result.State); Assert.Equal("backend_work_unconfirmed", result.ReasonCode);
            Assert.False(result.DeploymentVerified); Assert.Equal(0, clock.Reads);
            Assert.Equal(before, Snapshot(f)); Assert.True(Assert.Single(Journal(f).Entries).PhysicalOutstanding);
            Assert.Equal(1, f.Backend.Count);
        }
        finally { clock.Read = null; release.TrySetResult(); backend.Probe = null; }
    }

    [Fact]
    public async Task AlreadyStoppedProbeNeverReadsBrokenClock()
    {
        await using var f = new SceneFixture(); var (clock, _) = await Wrap(f); await f.Service.DisposeAsync();
        clock.Reads = 0; clock.Read = _ => throw new IOException(Secret);
        var result = await f.Service.ProbeAsync(TestContext.Current.CancellationToken);
        Assert.Equal("disabled", result.State); Assert.False(result.DeploymentVerified); Assert.Equal(0, clock.Reads);
    }

    [Fact]
    public async Task OrdinaryBackendFailureKeepsKnownBytesAndHash()
    {
        await using var f = new SceneFixture(); var (_, backend) = await Wrap(f);
        backend.Probe = _ => ValueTask.FromException<SceneReadiness>(new IOException(Secret));
        var expected = new SceneReadiness("unavailable", "probe_failed", false, f.Backend.Identity);
        var result = await f.Service.ProbeAsync(TestContext.Current.CancellationToken);
        Assert.Equal(SceneJson.Write(expected), SceneJson.Write(result));
        var envelope = await f.Invoke("scene.capabilities"); Assert.True(envelope.Succeeded);
        var catalog = SceneJson.Read<SceneCapabilityCatalog>(envelope.Metadata[SceneService.ResultMetadataKey], 128 * 1024);
        Assert.Equal(SceneJson.Write(expected), SceneJson.Write(catalog.Readiness));
        Assert.Equal(SceneJson.Hash(SceneJson.Write(catalog with { Readiness = expected })), envelope.OutputHash);
        Assert.Equal(0, f.Backend.Count);
    }

    private static Exception Fault(string kind, CancellationToken token)
    {
        Exception error = kind switch
        {
            "custom" => new SceneException(Secret, true),
            "known" => new SceneException("backend_oom", true),
            "cancel" => new OperationCanceledException(Secret, new IOException(Secret), token),
            "task" => new TaskCanceledException(Secret, new IOException(Secret), token),
            _ => new IOException(Secret, new InvalidOperationException(Secret))
        };
        error.Data["credential"] = Secret; return error;
    }
    private static async Task AssertCancellation(Task<SceneReadiness> pending, string kind, CancellationToken caller)
    {
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.True(pending.IsCanceled); Assert.False(pending.IsFaulted);
        if (kind == "task") Assert.Null(Assert.IsType<TaskCanceledException>(error).Task); else Assert.IsType<OperationCanceledException>(error);
        Assert.Equal("Scene request canceled.", error.Message); Assert.Equal(caller, error.CancellationToken);
        Assert.Null(error.InnerException); Assert.Empty(error.Data); Assert.DoesNotContain(Secret, error.ToString());
    }
    private static void AssertFailed(SceneReadiness result)
    {
        Assert.Equal("unavailable", result.State); Assert.Equal("probe_failed", result.ReasonCode);
        Assert.False(result.DeploymentVerified); Assert.Null(result.RetryAfterMs); Assert.DoesNotContain(Secret, SceneJson.Write(result));
    }
    private static void AssertClosedCache(SceneReadiness result)
    {
        Assert.Equal("unavailable", result.State); Assert.False(result.DeploymentVerified); Assert.Null(result.RetryAfterMs);
        Assert.Contains(result.ReasonCode, new[] { "probe_failed", "probe_required" }); Assert.DoesNotContain(Secret, SceneJson.Write(result));
    }
    private static async Task<(FaultClock Clock, ProbeBackend Backend)> Wrap(SceneFixture f)
    {
        await f.Service.DisposeAsync(); var clock = new FaultClock(f.Clock); var backend = new ProbeBackend(f.Backend);
        f.Service = new(f.Options, backend, f, clock); f.Session = f.Service.OpenSession(f.Principal()); return (clock, backend);
    }
    private static async Task<SceneCapabilityCatalog> Catalog(SceneFixture f) => SceneJson.Read<SceneCapabilityCatalog>(
        (await f.Invoke("scene.capabilities")).Metadata[SceneService.ResultMetadataKey], 128 * 1024);
    private static SceneJournalDocument Journal(SceneFixture f) => SceneJson.Read<SceneJournalDocument>(File.ReadAllText(Path.Combine(f.Directory, "jobs.json")), 1024 * 1024);
    private static string Snapshot(SceneFixture f) => File.ReadAllText(Path.Combine(f.Directory, "jobs.json")) + "\nCHECKPOINT\n" + File.ReadAllText(Path.Combine(f.Directory, "recovery-required"));
    private sealed class FaultClock(ManualClock inner) : TimeProvider
    {
        internal Func<int, DateTimeOffset>? Read;
        internal int Reads;
        public override DateTimeOffset GetUtcNow() => Read is { } read ? read(Interlocked.Increment(ref Reads)) : inner.GetUtcNow();
        public override long TimestampFrequency => inner.TimestampFrequency;
        public override long GetTimestamp() => inner.GetTimestamp();
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => inner.CreateTimer(callback, state, dueTime, period);
    }
    private sealed class ProbeBackend(FakeBackend inner) : ISceneReasonerBackend
    {
        internal Func<CancellationToken, ValueTask<SceneReadiness>>? Probe;
        internal bool FailIdentity;
        public SceneBackendIdentity Identity => FailIdentity ? throw new IOException(Secret) : inner.Identity;
        public ValueTask<SceneReadiness> ProbeAsync(CancellationToken token) => Probe is { } probe ? probe(token) : inner.ProbeAsync(token);
        public Task<SceneBackendReply> AnalyzeAsync(SceneBackendInput input, CancellationToken token) => inner.AnalyzeAsync(input, token);
        public ValueTask<string> RequestCancellationAsync(string id, CancellationToken token) => inner.RequestCancellationAsync(id, token);
    }
}
