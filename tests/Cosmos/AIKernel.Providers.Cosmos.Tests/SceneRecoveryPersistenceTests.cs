namespace AIKernel.Providers.Cosmos.Tests;

public sealed class SceneRecoveryPersistenceTests
{
    [Theory]
    [InlineData("failed", false, false)]
    [InlineData("failed", false, true)]
    [InlineData("failed", true, false)]
    [InlineData("failed", true, true)]
    [InlineData("canceled", false, false)]
    [InlineData("canceled", false, true)]
    [InlineData("canceled", true, false)]
    [InlineData("canceled", true, true)]
    [InlineData("timed_out", false, false)]
    [InlineData("timed_out", false, true)]
    [InlineData("timed_out", true, false)]
    [InlineData("timed_out", true, true)]
    public async Task FailedConfirmationCannotEraseLeasesThroughLaterHostWrites(string terminal, bool loaded, bool expired)
    {
        await using var f = new SceneFixture(); var request = await Outstanding(f, terminal);
        if (loaded) await f.Restart();
        var original = await ReadJournal(f); Assert.True(Assert.Single(original.Entries).PhysicalOutstanding);
        FailConfirmation(f, attempts: 2);
        // Status, cancel, catalog and invalidation must not implicitly recover.
        Assert.Equal(terminal, (await f.Status()).Status);
        Assert.True((await f.Invoke("scene.cancel", Id(request.RequestId))).Succeeded);
        Assert.True((await f.Invoke("scene.invalidate")).Succeeded);
        await AssertQuarantined(f, request);
        // This operation always saves, even when the original request is terminal.
        f.Service.RevokeRequestAuthorization("caller-a", request.RequestId);
        var saved = await ReadJournal(f);
        Assert.Equal(SceneJson.Write(original), SceneJson.Write(saved));
        if (expired) f.Clock.Advance(25 * 60 * 60 * 1000);
        for (int restart = 0; restart < 2; restart++)
        {
            await f.Restart();
            Assert.True(Assert.Single((await ReadJournal(f)).Entries).PhysicalOutstanding);
            await AssertQuarantined(f, request);
        }
        Assert.Equal(1, f.Backend.Count);
    }

    [Theory]
    [InlineData("confirmed", "confirmed")]
    [InlineData("unsupported", "unsupported")]
    [InlineData("private-cancel-detail", "unconfirmed")]
    public async Task FailedConfirmationCannotEraseLeasesThroughLateCancellationWrites(string receipt, string expected)
    {
        await using var f = new SceneFixture(); var backend = new DelayedCancellation(f.Backend);
        await f.Service.DisposeAsync(); f.Service = new(f.Options, backend, f, f.Clock);
        f.Session = f.Service.OpenSession(f.Principal()); f.Backend.Block = true;
        var request = await f.Request(); await f.Submit(request); await f.WaitForCall();
        await f.Invoke("scene.cancel", Id(request.RequestId));
        await backend.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        try
        {
            f.Backend.FailAll(new("backend_transport_unconfirmed", true)); await Idle(f);
            var original = Assert.Single((await ReadJournal(f)).Entries);
            Assert.True(original.PhysicalOutstanding); Assert.Equal("requested", original.Result.BackendCancellation);
            FailConfirmation(f);
            backend.Receipt.TrySetResult(receipt);
            await SceneFixture.Eventually(async () => (await f.Status()).BackendCancellation == expected);
            var saved = Assert.Single((await ReadJournal(f)).Entries);
            Assert.True(saved.PhysicalOutstanding);
            Assert.Equal(SceneJson.Write(original with { Result = original.Result with { BackendCancellation = expected } }), SceneJson.Write(saved));
            await AssertQuarantined(f, request);
            await f.Restart(); await AssertQuarantined(f, request);
            Assert.True(Assert.Single((await ReadJournal(f)).Entries).PhysicalOutstanding);
            Assert.Equal(1, f.Backend.Count);
        }
        finally { backend.Receipt.TrySetResult("unsupported"); }
    }

    [Fact]
    public async Task FailedConfirmationPreservesEveryLeaseWhileAnotherCompletedResultIsRevoked()
    {
        await using var f = new SceneFixture();
        await f.Service.DisposeAsync();
        f.Options = new() { Enabled = true, JournalDirectory = f.Directory, MaxConcurrentInference = 2 };
        f.Service = new(f.Options, f.Backend, f, f.Clock); f.Session = f.Service.OpenSession(f.Principal());
        await f.Submit(await f.Request("completed")); Assert.Single((await f.Completed("completed")).Proposals); await Idle(f);
        f.Backend.Block = true;
        var a = await f.Request("lease-a"); await f.Submit(a); await f.WaitForCall();
        var other = f.Service.OpenSession(f.Principal("caller-b"));
        var b = await f.Request("lease-b", other); await f.Submit(b, other);
        await SceneFixture.Eventually(() => Task.FromResult(f.Backend.Count == 3));
        f.Backend.FailAll(new("backend_transport_unconfirmed", true)); await Idle(f);
        await SceneFixture.Eventually(async () => (await f.Status("lease-a")).BackendCancellation != "requested"
            && (await f.Status("lease-b", other)).BackendCancellation != "requested");
        var original = await ReadJournal(f);
        Assert.Equal(2, original.Entries.Count(e => e.PhysicalOutstanding));
        FailConfirmation(f);
        f.Service.RevokeRequestAuthorization("caller-a", "completed");
        var saved = await ReadJournal(f);
        Assert.Equal(2, saved.Entries.Count(e => e.PhysicalOutstanding));
        foreach (var lease in original.Entries.Where(e => e.PhysicalOutstanding))
            Assert.Equal(SceneJson.Write(lease), SceneJson.Write(Assert.Single(saved.Entries, e => e.Key == lease.Key)));
        var completed = Assert.Single(saved.Entries, e => e.Result.RequestId == "completed");
        Assert.False(completed.PhysicalOutstanding); Assert.Empty(completed.Result.Proposals);
        await f.Restart(); await AssertQuarantined(f, a);
        Assert.Equal(2, (await ReadJournal(f)).Entries.Count(e => e.PhysicalOutstanding));
        Assert.Equal(3, f.Backend.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuccessfulRetryCommitsOnlyTheLeaseChangeAndDoesNotReplayOldInference(bool loaded)
    {
        await using var f = new SceneFixture(); var request = await Outstanding(f, "failed");
        if (loaded) await f.Restart();
        FailConfirmation(f, attempts: 2);
        f.Service.RevokeRequestAuthorization("caller-a", request.RequestId);
        await AssertQuarantined(f, request);
        var original = await ReadJournal(f);
        Assert.True(Assert.Single(original.Entries).PhysicalOutstanding);
        var reply = await f.Invoke("scene.status", Id(request.RequestId));
        f.Service.ConfirmBackendQuiescent();
        var saved = await ReadJournal(f);
        Assert.Equal(SceneJson.Write(original with { Entries = original.Entries.Select(e => e with { PhysicalOutstanding = false }).ToArray() }), SceneJson.Write(saved));
        var after = await f.Invoke("scene.status", Id(request.RequestId));
        Assert.Equal(reply.Metadata[SceneService.ResultMetadataKey], after.Metadata[SceneService.ResultMetadataKey]);
        Assert.Equal(reply.OutputHash, after.OutputHash);
        var readiness = (await Catalog(f)).Readiness;
        Assert.Equal("unavailable", readiness.State); Assert.Equal("probe_required", readiness.ReasonCode);
        Assert.False(readiness.DeploymentVerified);
        for (int restart = 0; restart < 2; restart++)
        {
            await f.Restart(); Assert.False(Assert.Single((await ReadJournal(f)).Entries).PhysicalOutstanding);
            Assert.Equal("ready", (await f.Service.ProbeAsync(TestContext.Current.CancellationToken)).State);
        }
        Assert.Equal(1, f.Backend.Count);
        Assert.True((await f.Submit(await f.Request("next-request"))).Succeeded);
        Assert.Single((await f.Completed("next-request")).Proposals);
        Assert.Equal("failed", (await f.Status()).Status); Assert.Equal(2, f.Backend.Count);
    }

    [Fact]
    public async Task ActiveInferenceStillRejectsConfirmationWithoutChangingTheJournal()
    {
        await using var f = new SceneFixture(); f.Backend.Block = true;
        await f.Submit(await f.Request()); await f.WaitForCall();
        var original = await ReadJournal(f);
        Assert.True(Assert.Single(original.Entries).PhysicalOutstanding);
        var error = Assert.Throws<SceneException>(f.Service.ConfirmBackendQuiescent);
        Assert.Equal("backend_still_active", error.Code); Assert.Null(error.InnerException); Assert.Empty(error.Data);
        Assert.Equal(SceneJson.Write(original), SceneJson.Write(await ReadJournal(f)));
        Assert.Equal(1, (await Catalog(f)).ActiveInference); Assert.Equal(1, f.Backend.Count);
    }

    private static async Task<SceneRequest> Outstanding(SceneFixture f, string terminal)
    {
        f.Backend.Block = true; var request = await f.Request(); await f.Submit(request); await f.WaitForCall();
        if (terminal == "canceled") await f.Invoke("scene.cancel", Id(request.RequestId));
        if (terminal == "timed_out") f.Clock.Advance(3100);
        f.Backend.FailAll(new("backend_transport_unconfirmed", true));
        Assert.Equal(terminal, (await f.Completed()).Status); await Idle(f);
        await SceneFixture.Eventually(async () => (await f.Status()).BackendCancellation != "requested");
        return request;
    }
    private static void FailConfirmation(SceneFixture f, int attempts = 1)
    {
        var directory = Path.GetFullPath(f.Directory);
        var journal = Path.Combine(directory, "jobs.json"); var backup = Path.Combine(directory, "confirmation-test-backup.json");
        Assert.Equal(directory, Path.GetDirectoryName(journal)); Assert.Equal(directory, Path.GetDirectoryName(backup));
        // The fixture has no active inference/writer during this obstruction.
        File.Move(journal, backup); Directory.CreateDirectory(journal);
        try
        {
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                var error = Assert.Throws<SceneException>(f.Service.ConfirmBackendQuiescent);
                Assert.Equal("journal_unavailable", error.Code); Assert.Equal(error.Code, error.Message);
                Assert.Null(error.InnerException); Assert.Empty(error.Data);
            }
        }
        finally { Directory.Delete(journal); File.Move(backup, journal); }
    }
    private static async Task AssertQuarantined(SceneFixture f, SceneRequest request)
    {
        var readiness = await f.Service.ProbeAsync(TestContext.Current.CancellationToken);
        Assert.Equal("degraded", readiness.State); Assert.Equal("backend_work_unconfirmed", readiness.ReasonCode);
        Assert.False(readiness.DeploymentVerified);
        var catalog = await Catalog(f); Assert.Equal("backend_work_unconfirmed", catalog.Readiness.ReasonCode);
        Assert.Equal(0, catalog.ActiveInference);
        var blocked = await f.Submit(request with { RequestId = "blocked-request" });
        Assert.False(blocked.Succeeded); Assert.Equal("backend_work_unconfirmed", blocked.ErrorCode);
        Assert.Equal("backend_work_unconfirmed", (await Assert.ThrowsAsync<SceneException>(() => f.Request("blocked-media"))).Code);
    }
    private static Dictionary<string, string> Id(string id) => new() { ["requestId"] = id };
    private static async Task<SceneCapabilityCatalog> Catalog(SceneFixture f) => SceneJson.Read<SceneCapabilityCatalog>(
        (await f.Invoke("scene.capabilities")).Metadata[SceneService.ResultMetadataKey], 128 * 1024);
    private static Task Idle(SceneFixture f) => SceneFixture.Eventually(async () => (await Catalog(f)).ActiveInference == 0);
    private static async Task<SceneJournalDocument> ReadJournal(SceneFixture f) => SceneJson.Read<SceneJournalDocument>(
        await File.ReadAllTextAsync(Path.Combine(f.Directory, "jobs.json"), TestContext.Current.CancellationToken), 1024 * 1024);
    private sealed class DelayedCancellation(FakeBackend inner) : ISceneReasonerBackend
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<string> Receipt = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public SceneBackendIdentity Identity => inner.Identity;
        public ValueTask<SceneReadiness> ProbeAsync(CancellationToken token) => inner.ProbeAsync(token);
        public Task<SceneBackendReply> AnalyzeAsync(SceneBackendInput input, CancellationToken token) => inner.AnalyzeAsync(input, token);
        public ValueTask<string> RequestCancellationAsync(string id, CancellationToken token)
        { Entered.TrySetResult(); return new(Receipt.Task); }
    }
}
