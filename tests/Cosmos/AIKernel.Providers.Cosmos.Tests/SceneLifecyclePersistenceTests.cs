namespace AIKernel.Providers.Cosmos.Tests;

public sealed class SceneLifecyclePersistenceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task IntakeFailureCannotDispatchOrLoseQuarantine(bool laterWrite, bool expired)
    {
        await using var f = new SceneFixture(); var request = await f.Request();
        using (new ObstructJournal(f))
        {
            var response = await f.Submit(request);
            Assert.False(response.Succeeded); Assert.Equal("journal_unavailable", response.ErrorCode);
            Assert.Equal(response.ErrorCode, response.ErrorMessage); Assert.Empty(response.Metadata);
        }
        Assert.Equal(0, f.Backend.Count);
        if (laterWrite) f.Service.RevokeRequestAuthorization("caller-a", request.RequestId);
        await AssertQuarantined(f, request);
        if (expired) f.Clock.Advance(25 * 60 * 60 * 1000);
        await RestartTwice(f, request, physical: false);
        Assert.Equal(0, f.Backend.Count);
    }

    [Fact]
    public async Task FailedDispatchReservationNeverStartsAfterRecovery()
    {
        await using var f = new SceneFixture(); await f.Service.DisposeAsync();
        var clock = new BeforeTimerClock(f.Clock);
        f.Service = new(f.Options, f.Backend, f, clock); f.Session = f.Service.OpenSession(f.Principal());
        var request = await f.Request(); ObstructJournal? obstruction = null;
        clock.BeforeTimer = () => obstruction = new(f);
        try
        {
            var reply = await f.Submit(request);
            Assert.False(reply.Succeeded); Assert.Equal("journal_unavailable", reply.ErrorCode);
        }
        finally { obstruction?.Dispose(); clock.BeforeTimer = null; }
        Assert.Equal(0, f.Backend.Count);
        await AssertQuarantined(f, request);
        var status = await f.Status();
        Assert.Equal("failed", status.Status); Assert.Equal("journal_unavailable", status.ErrorCode);
        Assert.Empty(status.Proposals);
        f.Service.RevokeRequestAuthorization("caller-a", request.RequestId);
        Assert.False(Assert.Single((await Journal(f)).Entries).PhysicalOutstanding);
        await RestartTwice(f, request, physical: false);
        f.Service.ConfirmBackendQuiescent();
        Assert.True((await f.Submit(await f.Request("new-request"))).Succeeded);
        Assert.Equal("completed", (await f.Completed("new-request")).Status);
        Assert.Equal(1, f.Backend.Count);
        Assert.Equal("failed", (await f.Status()).Status);
    }

    [Theory]
    [InlineData("cancel", false)]
    [InlineData("cancel", true)]
    [InlineData("timeout", false)]
    [InlineData("timeout", true)]
    [InlineData("invalidate", false)]
    [InlineData("invalidate", true)]
    public async Task FailedTerminationRetainsLeaseAfterPhysicalReturn(string operation, bool unknown)
    {
        await using var f = new SceneFixture(); f.Backend.Block = true;
        var request = await f.Request(); await f.Submit(request); await f.WaitForCall();
        using (new ObstructJournal(f))
        {
            if (operation == "timeout") f.Clock.Advance(3100);
            else
            {
                var response = operation == "cancel" ? await f.Invoke("scene.cancel", Id()) : await f.Invoke("scene.invalidate");
                Assert.False(response.Succeeded); Assert.Equal("journal_unavailable", response.ErrorCode);
            }
            await SceneFixture.Eventually(async () => (await f.Status()).BackendCancellation == "unsupported");
        }
        if (unknown) f.Backend.FailAll(new("backend_transport_unconfirmed", true));
        else f.Backend.ReleaseAll();
        await Idle(f);
        var state = operation switch { "cancel" => "canceled", "timeout" => "timed_out", _ => "superseded" };
        Assert.Equal(state, (await f.Status()).Status); Assert.Empty((await f.Status()).Proposals);
        f.Service.RevokeRequestAuthorization("caller-a", request.RequestId);
        Assert.True(Assert.Single((await Journal(f)).Entries).PhysicalOutstanding);
        await RestartTwice(f, request, physical: true);
        Assert.Equal(1, f.Backend.Count);
    }

    [Theory]
    [InlineData("success", false)]
    [InlineData("success", true)]
    [InlineData("known-failure", false)]
    [InlineData("known-failure", true)]
    [InlineData("unknown-failure", false)]
    [InlineData("unknown-failure", true)]
    public async Task FailedCompletionCannotPublishCandidatesOrDropLease(string outcome, bool laterWrite)
    {
        await using var f = new SceneFixture(); f.Backend.Block = true;
        var request = await f.Request(); await f.Submit(request); await f.WaitForCall();
        using (new ObstructJournal(f))
        {
            if (outcome == "success") f.Backend.ReleaseAll();
            else f.Backend.FailAll(new(outcome == "known-failure" ? "backend_canceled" : "backend_transport_unconfirmed", outcome == "unknown-failure"));
            await Idle(f);
            var result = await f.Status();
            Assert.Equal("failed", result.Status); Assert.Empty(result.Proposals); Assert.False(result.FreshnessVerified);
            if (outcome == "success") Assert.Equal("journal_unavailable", result.ErrorCode);
        }
        if (laterWrite) f.Service.RevokeRequestAuthorization("caller-a", request.RequestId);
        Assert.True(Assert.Single((await Journal(f)).Entries).PhysicalOutstanding);
        await AssertQuarantined(f, request);
        f.Clock.Advance(25 * 60 * 60 * 1000);
        await RestartTwice(f, request, physical: true);
        Assert.Equal(1, f.Backend.Count);
    }

    [Fact]
    public async Task ConfirmRecoveryIsRequiredEvenWithoutAnyPhysicalLease()
    {
        await using var f = new SceneFixture(); var request = await f.Request();
        using (new ObstructJournal(f))
        {
            Assert.Equal("journal_unavailable", (await f.Submit(request)).ErrorCode);
            // Simulate shutdown while writes are still unavailable: no later save can repair the original intake.
            await f.Service.DisposeAsync();
        }
        await f.Restart(); await AssertQuarantined(f, request);
        Assert.Empty((await Journal(f)).Entries);
        using (new ObstructJournal(f))
            Assert.Equal("journal_unavailable", Assert.Throws<SceneException>(f.Service.ConfirmBackendQuiescent).Code);
        await f.Restart(); await AssertQuarantined(f, request);
        f.Service.ConfirmBackendQuiescent();
        Assert.Equal("probe_required", (await Catalog(f)).Readiness.ReasonCode);
        await f.Restart(); await f.Restart();
        Assert.Equal("ready", (await f.Service.ProbeAsync(TestContext.Current.CancellationToken)).State);
        Assert.True((await f.Submit(await f.Request("new-request"))).Succeeded);
        Assert.Equal("completed", (await f.Completed("new-request")).Status); Assert.Equal(1, f.Backend.Count);
    }

    [Fact]
    public async Task ProbeStartedBeforeWriteFailureCannotCertifyReadinessAfterIt()
    {
        await using var f = new SceneFixture(); var request = await f.Request();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Backend.BeforeProbe = async () => { entered.TrySetResult(); await release.Task; };
        var probe = f.Service.ProbeAsync(TestContext.Current.CancellationToken).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        try
        {
            using (new ObstructJournal(f)) Assert.Equal("journal_unavailable", (await f.Submit(request)).ErrorCode);
        }
        finally { release.TrySetResult(); f.Backend.BeforeProbe = null; }
        var readiness = await probe;
        Assert.Equal("degraded", readiness.State); Assert.Equal("backend_work_unconfirmed", readiness.ReasonCode);
        Assert.False(readiness.DeploymentVerified); Assert.Equal(0, f.Backend.Count);
    }

    private static async Task RestartTwice(SceneFixture f, SceneRequest request, bool physical)
    {
        for (int restart = 0; restart < 2; restart++)
        {
            await f.Restart(); await AssertQuarantined(f, request);
            Assert.Equal(physical, (await Journal(f)).Entries.Any(e => e.PhysicalOutstanding));
        }
    }
    private static async Task AssertQuarantined(SceneFixture f, SceneRequest request)
    {
        var readiness = await f.Service.ProbeAsync(TestContext.Current.CancellationToken);
        Assert.Equal("degraded", readiness.State); Assert.Equal("backend_work_unconfirmed", readiness.ReasonCode);
        Assert.False(readiness.DeploymentVerified);
        Assert.Equal("backend_work_unconfirmed", (await Catalog(f)).Readiness.ReasonCode);
        Assert.Equal("backend_work_unconfirmed", (await f.Submit(request with { RequestId = "blocked" })).ErrorCode);
        Assert.Equal("backend_work_unconfirmed", (await Assert.ThrowsAsync<SceneException>(() => f.Request("blocked-media"))).Code);
    }
    private static Dictionary<string, string> Id() => new() { ["requestId"] = "req-1" };
    private static async Task<SceneJournalDocument> Journal(SceneFixture f) => SceneJson.Read<SceneJournalDocument>(
        await File.ReadAllTextAsync(Path.Combine(f.Directory, "jobs.json"), TestContext.Current.CancellationToken), 1024 * 1024);
    private static async Task<SceneCapabilityCatalog> Catalog(SceneFixture f) => SceneJson.Read<SceneCapabilityCatalog>(
        (await f.Invoke("scene.capabilities")).Metadata[SceneService.ResultMetadataKey], 128 * 1024);
    private static Task Idle(SceneFixture f) => SceneFixture.Eventually(async () => (await Catalog(f)).ActiveInference == 0);
    private sealed class ObstructJournal : IDisposable
    {
        private readonly string path;
        private readonly string backup;
        internal ObstructJournal(SceneFixture f)
        {
            var directory = Path.GetFullPath(f.Directory);
            path = Path.Combine(directory, "jobs.json"); backup = Path.Combine(directory, "lifecycle-test-backup.json");
            Assert.Equal(directory, Path.GetDirectoryName(path)); Assert.Equal(directory, Path.GetDirectoryName(backup));
            File.Move(path, backup); Directory.CreateDirectory(path);
        }
        public void Dispose() { Directory.Delete(path); File.Move(backup, path); }
    }
    private sealed class BeforeTimerClock(ManualClock clock) : TimeProvider
    {
        internal Action? BeforeTimer;
        public override long TimestampFrequency => clock.TimestampFrequency;
        public override long GetTimestamp() => clock.GetTimestamp();
        public override DateTimeOffset GetUtcNow() => clock.GetUtcNow();
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        { BeforeTimer?.Invoke(); return clock.CreateTimer(callback, state, dueTime, period); }
    }
}
