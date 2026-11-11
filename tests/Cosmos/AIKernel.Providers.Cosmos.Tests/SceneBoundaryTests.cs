namespace AIKernel.Providers.Cosmos.Tests;

public sealed class SceneBoundaryTests
{
    [Fact]
    public async Task A12_MediaUploadsAlsoHaveBoundedConcurrency()
    {
        await using var f = new SceneFixture(); var request = await f.Request(); var png = SceneFixture.Png();
        using var canceled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var registration = new SceneMediaRegistration(SceneJson.Snapshot(request), "frame-busy", 0, "image/png", 2, 2,
            SceneJson.Hash(png), f.Clock.GetUtcNow().AddMinutes(1));
        using var first = new WaitingStream(); using var second = new WaitingStream();
        var one = f.Session.RegisterMediaAsync(registration, first, canceled.Token);
        var two = f.Session.RegisterMediaAsync(registration, second, canceled.Token);
        await Task.WhenAll(first.Entered.Task, second.Entered.Task).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<SceneException>(() => f.Session.RegisterMediaAsync(registration, new MemoryStream(png), TestContext.Current.CancellationToken));
        Assert.Equal("media_busy", error.Code);
        await canceled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => one);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => two);
    }

    private sealed class WaitingStream : MemoryStream
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { Entered.TrySetResult(); await Task.Delay(Timeout.Infinite, cancellationToken); return 0; }
    }

    [Theory]
    [InlineData("unknown", "invalid_unknown")]
    [InlineData("motion_observed", "temporal_evidence_required")]
    public async Task A03_A07_UnknownAndTemporalClaimsRequireValidEvidence(string reason, string code)
    {
        await using var f = new SceneFixture();
        f.Backend.Output = SceneJson.Write(new SceneModelResult([new("obs-1", "actor-a", "board-a", reason, false, ["frame-1"])],
            [new("actor-a", 12, "look_at", "board-a", ["obs-1"])]));
        await f.Submit(await f.Request()); var result = await f.Completed();
        Assert.Equal("failed", result.Status); Assert.Equal(code, result.ErrorCode); Assert.Empty(result.Proposals);
    }

    [Fact]
    public async Task A05_WallClockRollbackCannotRefreshCompletedProposal()
    {
        await using var f = new SceneFixture();
        await f.Submit(await f.Request()); Assert.Single((await f.Completed()).Proposals);
        f.Clock.WallOffsetMilliseconds = -10_000;
        f.Clock.Advance(6000);
        var result = await f.Status();
        Assert.Empty(result.Proposals); Assert.False(result.FreshnessVerified);
    }

    [Fact]
    public async Task A04_UnknownPhysicalLeaseSurvivesRestartAndIdempotencyExpiry()
    {
        await using var f = new SceneFixture();
        f.Backend.Failure = new SceneException("backend_transport_unconfirmed", true);
        var request = await f.Request(); await f.Submit(request); await f.Completed();
        f.Clock.Advance(25 * 60 * 60 * 1000);
        await f.Restart();
        Assert.Equal("backend_work_unconfirmed", (await f.Service.ProbeAsync(TestContext.Current.CancellationToken)).ReasonCode);
        Assert.Equal("backend_work_unconfirmed", (await f.Submit(request with { RequestId = "new-request" })).ErrorCode);
        Assert.Equal(1, f.Backend.Count);
        f.Service.ConfirmBackendQuiescent();
        Assert.Equal("ready", (await f.Service.ProbeAsync(TestContext.Current.CancellationToken)).State);
    }

    [Fact]
    public async Task A04_CanceledOutstandingLeaseIsDurableBeforeBackendCompletes()
    {
        await using var f = new SceneFixture(); f.Backend.Block = true;
        await f.Submit(await f.Request()); await f.WaitForCall();
        await f.Invoke("scene.cancel", new Dictionary<string, string> { ["requestId"] = "req-1" });
        var document = SceneJson.Read<SceneJournalDocument>(await File.ReadAllTextAsync(Path.Combine(f.Directory, "jobs.json"), TestContext.Current.CancellationToken), 1024 * 1024);
        Assert.True(Assert.Single(document.Entries).PhysicalOutstanding);
        Assert.Equal("canceled", document.Entries[0].Result.Status);
        Assert.Throws<SceneException>(f.Service.ConfirmBackendQuiescent);
    }

    [Fact]
    public async Task A02_TimeOrderedImageSequenceSharesOneSnapshot()
    {
        await using var f = new SceneFixture(); var request = await f.Request(); var png = SceneFixture.Png();
        var earlier = await f.Session.RegisterMediaAsync(new(SceneJson.Snapshot(request), "frame-earlier", -1000,
            "image/png", 2, 2, SceneJson.Hash(png), f.Clock.GetUtcNow().AddMinutes(1)), new MemoryStream(png), TestContext.Current.CancellationToken);
        request = request with { Media = [new(earlier.MediaId, "frame-earlier", "image/png", 2, 2, -1000), request.Media[0]], Mode = "observe-only" };
        Assert.True((await f.Submit(request)).Succeeded);
        var result = await f.Completed();
        Assert.Equal("completed", result.Status); Assert.Equal("observe-only", result.Mode); Assert.Empty(result.Proposals);
        var input = await f.Backend.Calls.Reader.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, input.Images.Count);
    }

    [Fact]
    public async Task A05_PolicyRevocationBlocksStatusAndDoesNotCreateNewInference()
    {
        await using var f = new SceneFixture(); await f.Submit(await f.Request()); await f.Completed();
        f.Policy = "policy-new";
        var result = await f.Invoke("scene.status", new Dictionary<string, string> { ["requestId"] = "req-1" });
        Assert.False(result.Succeeded); Assert.Equal("request_not_found", result.ErrorCode); Assert.Equal(1, f.Backend.Count);
    }

    [Fact]
    public async Task A06_ValidHashDoesNotMakeCorruptPngAcceptable()
    {
        await using var f = new SceneFixture(); var request = await f.Request(); var png = SceneFixture.Png(); png[29] ^= 1;
        await Assert.ThrowsAsync<SceneException>(() => f.Session.RegisterMediaAsync(new(SceneJson.Snapshot(request), "frame-corrupt", 0,
            "image/png", 2, 2, SceneJson.Hash(png), f.Clock.GetUtcNow().AddMinutes(1)), new MemoryStream(png), TestContext.Current.CancellationToken));
        Assert.Equal(0, f.Backend.Count);
    }

    [Fact]
    public async Task A03_DuplicateModelFieldsAreRejected()
    {
        await using var f = new SceneFixture(); f.Backend.Output = "{\"observations\":[],\"proposals\":[],\"proposals\":[]}";
        await f.Submit(await f.Request()); var result = await f.Completed();
        Assert.Equal("failed", result.Status); Assert.Equal("duplicate_field", result.ErrorCode); Assert.Empty(result.Proposals);
    }

    [Fact]
    public async Task A08_BackpressureReturnsRetryHintWithoutAutomaticRetry()
    {
        await using var f = new SceneFixture(); f.Backend.Failure = new SceneException("backend_busy");
        await f.Submit(await f.Request()); var result = await f.Completed();
        Assert.Equal("failed", result.Status); Assert.Equal("backend_busy", result.ErrorCode); Assert.Equal(1000, result.RetryAfterMs);
        Assert.Equal(1, f.Backend.Count);
    }

    [Fact]
    public async Task A04_PreCanceledEnvelopeNeverStartsInference()
    {
        await using var f = new SceneFixture(); var request = await f.Request();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await f.Session.InvokeAsync(new("inv-canceled", SceneService.CapabilityId,
            "scene.analyze", new Dictionary<string, string> { ["request_json"] = SceneJson.Write(request) }, null, null, new Dictionary<string, string>()), new CancellationToken(true)));
        Assert.Equal(0, f.Backend.Count);
    }
}
