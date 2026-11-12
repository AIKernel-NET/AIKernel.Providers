namespace AIKernel.Providers.Cosmos.Tests;

public sealed class SceneDisposedInstanceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task OldConfirmationCannotRewriteSuccessorsPhysicalWork(bool oldQuarantined, bool successorQuarantined)
    {
        await using var f = new SceneFixture();
        f.Backend.Failure = oldQuarantined ? new("backend_transport_unconfirmed", true) : null;
        await f.Submit(await f.Request()); await f.Completed(); await Idle(f);
        var old = f.Service; await f.Restart();
        if (oldQuarantined) f.Service.ConfirmBackendQuiescent();
        f.Backend.Failure = successorQuarantined ? new("backend_transport_unconfirmed", true) : null;
        f.Backend.Block = !successorQuarantined;
        await f.Submit(await f.Request("successor"));
        if (successorQuarantined) { await f.Completed("successor"); await Idle(f); }
        else await SceneFixture.Eventually(() => Task.FromResult(f.Backend.Count == 2));
        var before = Snapshot(f);
        var error = Record.Exception(old.ConfirmBackendQuiescent);
        Assert.Equal(before, Snapshot(f)); AssertDisabled(error);
        AssertDisabled(Record.Exception(() => old.RevokeRequestAuthorization("caller-a", "req-1")));
        await old.DisposeAsync(); await old.DisposeAsync();
        Assert.Equal(before, Snapshot(f)); AssertWriterOwned(f);
        var entry = Assert.Single(Journal(f).Entries, e => e.Result.RequestId == "successor");
        Assert.True(entry.PhysicalOutstanding);
        Assert.Equal(successorQuarantined ? "failed" : "running", entry.Result.Status);
        Assert.Equal(2, f.Backend.Count);
    }

    [Theory]
    [InlineData("confirm")]
    [InlineData("revoke")]
    [InlineData("session")]
    [InlineData("analyze")]
    [InlineData("status")]
    [InlineData("cancel")]
    [InlineData("invalidate")]
    [InlineData("capabilities")]
    [InlineData("media")]
    public async Task StoppedEntryPointsRejectBeforeHostCallbacks(string operation)
    {
        await using var f = new SceneFixture(); var backend = new ObservedBackend(f.Backend);
        await f.Service.DisposeAsync(); f.Service = new(f.Options, backend, f, f.Clock);
        f.Session = f.Service.OpenSession(f.Principal());
        var request = await f.Request(); var old = f.Service; var session = f.Session;
        await f.Restart(); var before = Snapshot(f);
        int authorizationCalls = 0; int identities = backend.IdentityReads;
        f.Authorization = (_, grant) => { authorizationCalls++; return grant; };
        Exception? error = null;
        switch (operation)
        {
            case "confirm": error = Record.Exception(old.ConfirmBackendQuiescent); break;
            case "revoke": error = Record.Exception(() => old.RevokeRequestAuthorization("caller-a", request.RequestId)); break;
            case "session": error = Record.Exception(() => old.OpenSession(f.Principal())); break;
            case "media":
                var png = SceneFixture.Png(); using (var stream = new UnreadableStream())
                    error = await Record.ExceptionAsync(() => session.RegisterMediaAsync(new(SceneJson.Snapshot(request), "frame-1", 0,
                        "image/png", 2, 2, SceneJson.Hash(png), f.Clock.GetUtcNow().AddMinutes(1)), stream, TestContext.Current.CancellationToken));
                break;
            default:
                var result = operation == "analyze" ? await f.Submit(request, session)
                    : await f.Invoke("scene." + operation, operation is "status" or "cancel" ? Id(request.RequestId) : null, session);
                Assert.False(result.Succeeded); Assert.Equal("disabled", result.ErrorCode);
                Assert.Equal(result.ErrorCode, result.ErrorMessage); Assert.Empty(result.Metadata); Assert.Null(result.OutputHash);
                break;
        }
        Assert.Equal(before, Snapshot(f));
        Assert.Equal(0, authorizationCalls); Assert.Equal(identities, backend.IdentityReads);
        if (operation is "confirm" or "revoke" or "session" or "media") AssertDisabled(error);
        AssertWriterOwned(f); Assert.Equal(0, f.Backend.Count);
    }

    [Fact]
    public async Task StoppedProbeDoesNotRetainDeploymentApproval()
    {
        await using var f = new SceneFixture(); Assert.True((await f.Service.ProbeAsync(TestContext.Current.CancellationToken)).DeploymentVerified);
        var old = f.Service; await f.Restart(); var before = Snapshot(f);
        var result = await old.ProbeAsync(TestContext.Current.CancellationToken);
        AssertStopped(result); Assert.Equal(before, Snapshot(f)); AssertWriterOwned(f);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("failure")]
    [InlineData("cancel")]
    [InlineData("task")]
    public async Task LateProbeCannotReviveStoppedInstance(string outcome)
    {
        await using var f = new SceneFixture(); var backend = new ObservedBackend(f.Backend);
        await f.Service.DisposeAsync(); f.Service = new(f.Options, backend, f, f.Clock);
        f.Session = f.Service.OpenSession(f.Principal());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Backend.BeforeProbe = async () =>
        {
            entered.TrySetResult(); await release.Task;
            if (outcome == "failure") throw new IOException("private-probe-detail");
            if (outcome == "cancel") throw new OperationCanceledException("private-probe-detail");
            if (outcome == "task") throw new TaskCanceledException("private-probe-detail");
        };
        var old = f.Service; var pending = old.ProbeAsync(TestContext.Current.CancellationToken).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await old.DisposeAsync(); f.Backend.BeforeProbe = null;
        await f.Restart(); var before = Snapshot(f); int identities = backend.IdentityReads;
        release.TrySetResult();
        if (outcome is "cancel" or "task")
        {
            var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            Assert.Equal("Scene request canceled.", error.Message); Assert.Null(error.InnerException); Assert.Empty(error.Data);
            Assert.Equal(TestContext.Current.CancellationToken, error.CancellationToken);
            if (outcome == "task") Assert.Null(Assert.IsType<TaskCanceledException>(error).Task);
        }
        else AssertStopped(await pending);
        AssertStopped(await old.ProbeAsync(TestContext.Current.CancellationToken));
        Assert.Equal(identities, backend.IdentityReads); Assert.Equal(before, Snapshot(f)); AssertWriterOwned(f);
    }

    [Fact]
    public async Task ConcurrentDisposalWaitsForTheSamePhysicalDrainAndLeaseRelease()
    {
        await using var f = new SceneFixture(); f.Backend.Block = true;
        await f.Submit(await f.Request()); await f.WaitForCall();
        var old = f.Service; var first = old.DisposeAsync().AsTask();
        try
        {
            AssertStopped(await old.ProbeAsync(TestContext.Current.CancellationToken));
            var second = old.DisposeAsync().AsTask();
            Assert.False(first.IsCompleted); Assert.False(second.IsCompleted); Assert.Same(first, second);
            AssertDisabled(Record.Exception(old.ConfirmBackendQuiescent));
            AssertDisabled(Record.Exception(() => old.RevokeRequestAuthorization("caller-a", "req-1")));
            AssertWriterOwned(f);
            f.Backend.ReleaseAll(); await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            await f.Restart(); var before = Snapshot(f);
            await old.DisposeAsync(); Assert.Equal(before, Snapshot(f)); AssertWriterOwned(f);
        }
        finally { f.Backend.ReleaseAll(); await first; }
    }

    [Fact]
    public async Task FailedShutdownIsSharedAndCannotRetryCleanupAgainstSuccessor()
    {
        await using var f = new SceneFixture(); var old = f.Service;
        var marker = Path.Combine(Path.GetFullPath(f.Directory), "recovery-required");
        Assert.Equal(Path.GetFullPath(f.Directory), Path.GetDirectoryName(marker));
        // Only a fixture-owned path is obstructed. The successor must retain it.
        File.Delete(marker); Directory.CreateDirectory(marker);
        var shutdown = old.DisposeAsync().AsTask();
        var error = await Assert.ThrowsAsync<SceneException>(() => shutdown);
        Assert.Equal("journal_unavailable", error.Code); Assert.Null(error.InnerException); Assert.Empty(error.Data);
        f.Service = new(f.Options, f.Backend, f, f.Clock); f.Session = f.Service.OpenSession(f.Principal());
        var before = File.ReadAllText(Path.Combine(f.Directory, "jobs.json"));
        var retry = old.DisposeAsync().AsTask(); Assert.Same(shutdown, retry);
        Assert.Equal("journal_unavailable", (await Assert.ThrowsAsync<SceneException>(() => retry)).Code);
        AssertDisabled(Record.Exception(old.ConfirmBackendQuiescent));
        Assert.Equal(before, File.ReadAllText(Path.Combine(f.Directory, "jobs.json")));
        Assert.True(Directory.Exists(marker)); AssertWriterOwned(f);
        Assert.Equal("backend_work_unconfirmed", (await f.Service.ProbeAsync(TestContext.Current.CancellationToken)).ReasonCode);
    }

    [Theory]
    [InlineData("confirm")]
    [InlineData("probe")]
    public async Task IdentityCallbackThatStopsServiceCannotContinueAfterTransfer(string operation)
    {
        await using var f = new SceneFixture(); var backend = new ObservedBackend(f.Backend);
        await f.Service.DisposeAsync(); f.Service = new(f.Options, backend, f, f.Clock);
        f.Session = f.Service.OpenSession(f.Principal()); var old = f.Service; string? before = null;
        backend.BeforeIdentity = () =>
        {
            backend.BeforeIdentity = null;
            old.DisposeAsync().AsTask().GetAwaiter().GetResult();
            f.Service = new(f.Options, f.Backend, f, f.Clock); f.Session = f.Service.OpenSession(f.Principal());
            before = Snapshot(f);
        };
        if (operation == "confirm") AssertDisabled(Record.Exception(old.ConfirmBackendQuiescent));
        else AssertStopped(await old.ProbeAsync(TestContext.Current.CancellationToken));
        Assert.NotNull(before); Assert.Equal(before, Snapshot(f)); AssertWriterOwned(f);
    }

    private static void AssertStopped(SceneReadiness readiness)
    {
        Assert.Equal("disabled", readiness.State); Assert.Equal("disabled", readiness.ReasonCode);
        Assert.False(readiness.DeploymentVerified); Assert.Null(readiness.RetryAfterMs);
    }

    [Theory]
    [InlineData("failure")]
    [InlineData("cancel")]
    [InlineData("task")]
    public async Task PreparationFailurePreservesCancellationStateAndWriterOwnership(string kind)
    {
        await using var f = new SceneFixture(); var clock = new StopClock(f.Clock);
        await f.Service.DisposeAsync(); f.Service = new(f.Options, f.Backend, f, clock);
        f.Session = f.Service.OpenSession(f.Principal()); f.Backend.Block = true;
        await f.Submit(await f.Request()); await f.WaitForCall();
        clock.Error = kind switch
        {
            "cancel" => new OperationCanceledException("private-timer-detail"),
            "task" => new TaskCanceledException("private-timer-detail"),
            _ => new InvalidOperationException("private-timer-detail")
        };
        try
        {
            var shutdown = f.Service.DisposeAsync().AsTask();
            var error = await Record.ExceptionAsync(() => shutdown);
            Assert.NotNull(error); Assert.Null(error.InnerException); Assert.Empty(error.Data);
            Assert.Equal(kind != "failure", shutdown.IsCanceled); Assert.Equal(kind == "failure", shutdown.IsFaulted);
            if (kind == "failure") Assert.Equal("host_operation_failed", Assert.IsType<SceneException>(error).Code);
            else
            {
                Assert.Equal("Scene request canceled.", error.Message);
                Assert.Equal(CancellationToken.None, Assert.IsAssignableFrom<OperationCanceledException>(error).CancellationToken);
                if (kind == "task") Assert.Null(Assert.IsType<TaskCanceledException>(error).Task);
            }
            AssertWriterOwned(f);
        }
        finally { clock.Error = null; f.Backend.ReleaseAll(); }
        await f.Service.DisposeAsync(); await f.Restart(); AssertWriterOwned(f);
    }
    private static void AssertDisabled(Exception? error)
    {
        var scene = Assert.IsType<SceneException>(error);
        Assert.Equal("disabled", scene.Code); Assert.Equal(scene.Code, scene.Message);
        Assert.Null(scene.InnerException); Assert.Empty(scene.Data); Assert.False(scene.WorkMayStillBeRunning);
    }
    private static void AssertWriterOwned(SceneFixture f)
    {
        var error = Assert.Throws<SceneException>(() => new SceneService(f.Options, f.Backend, f, f.Clock));
        Assert.Equal("journal_unavailable", error.Code); Assert.Null(error.InnerException); Assert.Empty(error.Data);
    }
    private static Dictionary<string, string> Id(string id) => new() { ["requestId"] = id };
    private static SceneJournalDocument Journal(SceneFixture f) => SceneJson.Read<SceneJournalDocument>(File.ReadAllText(Path.Combine(f.Directory, "jobs.json")), 1024 * 1024);
    private static string Snapshot(SceneFixture f) => File.ReadAllText(Path.Combine(f.Directory, "jobs.json")) + "\nCHECKPOINT\n" + File.ReadAllText(Path.Combine(f.Directory, "recovery-required"));
    private static Task Idle(SceneFixture f) => SceneFixture.Eventually(async () =>
        SceneJson.Read<SceneCapabilityCatalog>((await f.Invoke("scene.capabilities")).Metadata[SceneService.ResultMetadataKey], 128 * 1024).ActiveInference == 0);
    private sealed class ObservedBackend(FakeBackend inner) : ISceneReasonerBackend
    {
        internal int IdentityReads;
        internal Action? BeforeIdentity;
        public SceneBackendIdentity Identity { get { Interlocked.Increment(ref IdentityReads); BeforeIdentity?.Invoke(); return inner.Identity; } }
        public ValueTask<SceneReadiness> ProbeAsync(CancellationToken token) => inner.ProbeAsync(token);
        public Task<SceneBackendReply> AnalyzeAsync(SceneBackendInput input, CancellationToken token) => inner.AnalyzeAsync(input, token);
        public ValueTask<string> RequestCancellationAsync(string id, CancellationToken token) => inner.RequestCancellationAsync(id, token);
    }
    private sealed class UnreadableStream : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("A stopped service must not read media.");
    }
    private sealed class StopClock(ManualClock clock) : TimeProvider
    {
        internal Exception? Error;
        public override long TimestampFrequency => clock.TimestampFrequency;
        public override long GetTimestamp() => clock.GetTimestamp();
        public override DateTimeOffset GetUtcNow() => clock.GetUtcNow();
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
            => new StopTimer(this, clock.CreateTimer(callback, state, dueTime, period));
        private sealed class StopTimer(StopClock owner, ITimer timer) : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => timer.Change(dueTime, period);
            public void Dispose() { if (owner.Error is { } error) throw error; timer.Dispose(); }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
