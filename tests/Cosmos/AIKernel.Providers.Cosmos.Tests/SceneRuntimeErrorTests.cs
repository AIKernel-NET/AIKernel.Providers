namespace AIKernel.Providers.Cosmos.Tests;

public sealed class SceneRuntimeErrorTests
{
    private const string Secret = "private-runtime-token-DO-NOT-RETURN";

    [Theory]
    [InlineData("custom")]
    [InlineData("prefix")]
    [InlineData("case")]
    [InlineData("whitespace")]
    [InlineData("newline")]
    [InlineData("empty")]
    [InlineData("null")]
    [InlineData("long")]
    [InlineData("ready")]
    [InlineData("offline_demo")]
    public async Task A10_UnknownJobCodesAreSanitizedBeforeStorageAndReply(string kind)
    {
        await using var f = new SceneFixture();
        f.Backend.Failure = new SceneException(Code(kind)); f.Backend.Failure.Data["credential"] = Secret;
        var request = await f.Request(); Assert.True((await f.Submit(request)).Succeeded);
        var result = await f.Completed();
        Assert.Equal("failed", result.Status); Assert.Equal("invalid_backend_response", result.ErrorCode);
        Assert.Empty(result.Proposals); Assert.False(result.FreshnessVerified);
        await Idle(f); await AssertJournalSafe(f);
        f.Backend.Failure = null; await f.Restart();
        var restored = await f.Status(); Assert.Equal("invalid_backend_response", restored.ErrorCode);
        Assert.Equal(SceneJson.Write(restored), SceneJson.Write(SceneFixture.Parse(await f.Submit(request))));
        Assert.Equal(1, f.Backend.Count);
    }

    [Theory]
    [InlineData("custom")]
    [InlineData("prefix")]
    [InlineData("case")]
    [InlineData("whitespace")]
    [InlineData("newline")]
    [InlineData("empty")]
    [InlineData("null")]
    [InlineData("long")]
    [InlineData("permission_denied")]
    [InlineData("invalid_json")]
    public async Task A09_UnknownReadinessReasonsCannotCertifyDeployment(string kind)
    {
        await using var f = new SceneFixture(); var backend = await Wrap(f);
        backend.Probe = _ => ValueTask.FromResult(new SceneReadiness("ready", Code(kind), true, backend.Identity, 1234));
        AssertUnavailable(await f.Service.ProbeAsync(TestContext.Current.CancellationToken));
        AssertUnavailable((await Catalog(f)).Readiness);
        await f.Submit(await f.Request()); var result = await f.Completed();
        Assert.Equal("failed", result.Status); Assert.Equal("probe_failed", result.ErrorCode);
        Assert.Empty(result.Proposals); Assert.Equal(0, backend.InferenceCalls);
        await Idle(f); await AssertJournalSafe(f);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A10_ProbeCancellationDoesNotReturnPrivateExceptionDetails(bool task)
    {
        await using var f = new SceneFixture(); var backend = await Wrap(f);
        using var internalToken = new CancellationTokenSource();
        OperationCanceledException original = task ? new TaskCanceledException(Secret, new IOException(Secret), internalToken.Token)
            : new OperationCanceledException(Secret, new IOException(Secret), internalToken.Token);
        original.Data["credential"] = Secret;
        backend.Probe = _ => ValueTask.FromException<SceneReadiness>(original);
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await f.Service.ProbeAsync(TestContext.Current.CancellationToken));
        if (task) Assert.IsType<TaskCanceledException>(error); else Assert.IsType<OperationCanceledException>(error);
        Assert.Null(error.InnerException); Assert.Empty(error.Data); Assert.DoesNotContain(Secret, error.ToString());
        Assert.Equal(TestContext.Current.CancellationToken, error.CancellationToken);
        Assert.Equal(0, backend.InferenceCalls);
    }

    [Theory]
    [InlineData("custom")]
    [InlineData("null")]
    public async Task A10_LegacyJournalErrorsAreSanitizedOnLoad(string kind)
    {
        await using var f = new SceneFixture();
        f.Backend.Failure = new("backend_busy"); var request = await f.Request(); await f.Submit(request);
        Assert.Equal("failed", (await f.Completed()).Status); await Idle(f);
        await f.Service.DisposeAsync(); // Fixture-owned file, with no active writer.
        var path = Path.Combine(f.Directory, "jobs.json");
        var journal = SceneJson.Read<SceneJournalDocument>(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken), 1024 * 1024);
        var entry = Assert.Single(journal.Entries);
        await File.WriteAllTextAsync(path, SceneJson.Write(journal with
        {
            Entries = [entry with { Result = entry.Result with { ErrorCode = Code(kind) } }]
        }), TestContext.Current.CancellationToken);
        f.Service = new(f.Options, f.Backend, f, f.Clock); f.Session = f.Service.OpenSession(f.Principal());
        Assert.Equal("invalid_backend_response", (await f.Status()).ErrorCode);
        await AssertJournalSafe(f);
        Assert.Equal("invalid_backend_response", SceneFixture.Parse(await f.Submit(request)).ErrorCode);
        Assert.Equal(1, f.Backend.Count);
    }

    [Theory]
    [InlineData("backend_busy", true, 1000)]
    [InlineData("backend_unavailable", true, 5000)]
    [InlineData("backend_oom", true, 5000)]
    [InlineData("response_truncated", false, null)]
    [InlineData("content_filtered", false, null)]
    [InlineData("invalid_json", false, null)]
    [InlineData("model_revision_mismatch", false, null)]
    [InlineData("backend_request_rejected", false, null)]
    [InlineData("backend_transport_unconfirmed", true, null)]
    [InlineData("backend_failed_unconfirmed", true, null)]
    public async Task A08_KnownJobCodesKeepRetryAndPhysicalQuarantine(string code, bool outstanding, int? retry)
    {
        await using var f = new SceneFixture(); f.Backend.Failure = new(code, outstanding);
        var request = await f.Request(); await f.Submit(request);
        Assert.Equal("failed", (await f.Completed()).Status); await Idle(f);
        var result = await f.Status(); Assert.Equal(code, result.ErrorCode); Assert.Equal(retry, result.RetryAfterMs);
        await AssertPhysical(f, outstanding);
        f.Backend.Failure = null;
        if (outstanding)
        {
            Assert.Equal("backend_work_unconfirmed", (await Assert.ThrowsAsync<SceneException>(() => f.Request("next"))).Code);
            var blocked = await f.Submit(request with { RequestId = "next" });
            Assert.False(blocked.Succeeded); Assert.Equal("backend_work_unconfirmed", blocked.ErrorCode);
            Assert.Equal(1, f.Backend.Count); f.Service.ConfirmBackendQuiescent();
        }
        var next = await f.Request("next");
        Assert.True((await f.Submit(next)).Succeeded); Assert.Single((await f.Completed("next")).Proposals);
        Assert.Equal(code, (await f.Status()).ErrorCode); Assert.Equal(2, f.Backend.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A12_UnknownCodeDoesNotChangePhysicalWorkEvidence(bool outstanding)
    {
        await using var f = new SceneFixture(); f.Backend.Failure = new(Secret, outstanding);
        var request = await f.Request(); await f.Submit(request); Assert.Equal("failed", (await f.Completed()).Status); await Idle(f);
        Assert.Equal("invalid_backend_response", (await f.Status()).ErrorCode); await AssertPhysical(f, outstanding);
        await AssertJournalSafe(f);
        f.Backend.Failure = null; await f.Restart();
        if (outstanding) Assert.Equal("backend_work_unconfirmed", (await Assert.ThrowsAsync<SceneException>(() => f.Request("next"))).Code);
        var next = outstanding ? request with { RequestId = "next" } : await f.Request("next");
        var accepted = await f.Submit(next);
        Assert.Equal(!outstanding, accepted.Succeeded);
        if (outstanding) { Assert.Equal("backend_work_unconfirmed", accepted.ErrorCode); Assert.Equal(1, f.Backend.Count); }
        else Assert.Single((await f.Completed("next")).Proposals);
    }

    [Theory]
    [InlineData("generic")]
    [InlineData("io")]
    [InlineData("cancel")]
    public async Task A12_UnstructuredBackendExceptionsRemainQuarantinedWithoutDetails(string kind)
    {
        await using var f = new SceneFixture(); var backend = await Wrap(f);
        backend.UnexpectedFailure = kind switch
        {
            "io" => new IOException(Secret), "cancel" => new TaskCanceledException(Secret), _ => new InvalidOperationException(Secret)
        };
        backend.UnexpectedFailure.Data["credential"] = Secret;
        var request = await f.Request(); await f.Submit(request); var result = await f.Completed();
        Assert.Equal("failed", result.Status); Assert.Equal("backend_failed_unconfirmed", result.ErrorCode);
        await Idle(f); await AssertPhysical(f, true); await AssertJournalSafe(f);
        Assert.Equal("backend_work_unconfirmed", (await Assert.ThrowsAsync<SceneException>(() => f.Request("blocked"))).Code);
        var blocked = await f.Submit(request with { RequestId = "blocked" });
        Assert.False(blocked.Succeeded); Assert.Equal("backend_work_unconfirmed", blocked.ErrorCode); Assert.Equal(1, backend.InferenceCalls);
    }

    [Theory]
    [InlineData("disabled", "deployment_disabled")]
    [InlineData("loading", "deployment_loading")]
    [InlineData("warming_up", "deployment_warming_up")]
    [InlineData("ready", "ready")]
    [InlineData("busy", "backend_busy")]
    [InlineData("degraded", "deployment_mismatch")]
    [InlineData("unavailable", "backend_oom")]
    public async Task A09_KnownReadinessAndRetryHintsArePreserved(string state, string reason)
    {
        await using var f = new SceneFixture(); var backend = await Wrap(f);
        var expected = new SceneReadiness(state, reason, state == "ready", backend.Identity, state == "ready" ? null : 1234);
        backend.Probe = _ => ValueTask.FromResult(expected);
        Assert.Equal(SceneJson.Write(expected), SceneJson.Write(await f.Service.ProbeAsync(TestContext.Current.CancellationToken)));
        Assert.Equal(SceneJson.Write(expected), SceneJson.Write((await Catalog(f)).Readiness));
        await f.Submit(await f.Request()); var result = await f.Completed();
        Assert.Equal(state == "ready" ? "completed" : "failed", result.Status);
        Assert.Equal(state == "ready" ? null : reason, result.ErrorCode);
        Assert.Equal(state == "ready" ? 1 : 0, backend.InferenceCalls);
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("loading")]
    [InlineData("warming_up")]
    [InlineData("busy")]
    [InlineData("degraded")]
    [InlineData("unavailable")]
    public async Task A09_UnknownReasonKeepsRecognizedNonReadyStateClosed(string state)
    {
        await using var f = new SceneFixture(); var backend = await Wrap(f);
        backend.Probe = _ => ValueTask.FromResult(new SceneReadiness(state, Secret, true, backend.Identity, 1234));
        var result = await f.Service.ProbeAsync(TestContext.Current.CancellationToken);
        Assert.Equal(state, result.State); Assert.Equal("probe_failed", result.ReasonCode);
        Assert.False(result.DeploymentVerified); Assert.Null(result.RetryAfterMs);
        Assert.DoesNotContain(Secret, SceneJson.Write((await Catalog(f)).Readiness));
        await f.Submit(await f.Request()); Assert.Equal("failed", (await f.Completed()).Status);
        Assert.Equal(0, backend.InferenceCalls);
    }

    [Theory]
    [InlineData("custom")]
    [InlineData("null")]
    [InlineData("READY")]
    public async Task A09_UnknownReadinessStateIsNotEchoedOrAccepted(string kind)
    {
        await using var f = new SceneFixture(); var backend = await Wrap(f);
        backend.Probe = _ => ValueTask.FromResult(new SceneReadiness(Code(kind), "ready", true, backend.Identity));
        AssertUnavailable(await f.Service.ProbeAsync(TestContext.Current.CancellationToken));
        AssertUnavailable((await Catalog(f)).Readiness);
        await f.Submit(await f.Request()); Assert.Equal("failed", (await f.Completed()).Status);
        Assert.Equal(0, backend.InferenceCalls);
    }

    [Theory]
    [InlineData("ready", "backend_busy", true, "unavailable")]
    [InlineData("ready", "ready", false, "unavailable")]
    [InlineData("busy", "ready", true, "busy")]
    public async Task A09_InconsistentReadyClaimsFailClosed(string state, string reason, bool verified, string expected)
    {
        await using var f = new SceneFixture(); var backend = await Wrap(f);
        backend.Probe = _ => ValueTask.FromResult(new SceneReadiness(state, reason, verified, backend.Identity, 1234));
        var result = await f.Service.ProbeAsync(TestContext.Current.CancellationToken);
        Assert.Equal(expected, result.State); Assert.Equal("probe_failed", result.ReasonCode);
        Assert.False(result.DeploymentVerified); Assert.Null(result.RetryAfterMs);
        await f.Submit(await f.Request()); Assert.Equal("failed", (await f.Completed()).Status);
        Assert.Equal(0, backend.InferenceCalls);
    }

    [Fact]
    public async Task A09_NullReadinessIsAClosedProbeFailure()
    {
        await using var f = new SceneFixture(); var backend = await Wrap(f);
        backend.Probe = _ => ValueTask.FromResult<SceneReadiness>(null!);
        AssertUnavailable(await f.Service.ProbeAsync(TestContext.Current.CancellationToken));
        await f.Submit(await f.Request()); Assert.Equal("probe_failed", (await f.Completed()).ErrorCode);
        Assert.Equal(0, backend.InferenceCalls);
    }

    [Fact]
    public async Task A09_ProviderMismatchStillRejectsAndDoesNotReturnUntrustedIdentity()
    {
        await using var f = new SceneFixture(); var backend = await Wrap(f);
        backend.Probe = _ => ValueTask.FromResult(new SceneReadiness("ready", Secret, true, backend.Identity with { Revision = Secret }));
        var result = await f.Service.ProbeAsync(TestContext.Current.CancellationToken);
        Assert.Equal("degraded", result.State); Assert.Equal("model_revision_mismatch", result.ReasonCode);
        Assert.False(result.DeploymentVerified); Assert.Equal(backend.Identity, result.Provider);
        Assert.DoesNotContain(Secret, SceneJson.Write(result));
        await f.Submit(await f.Request()); Assert.Equal("model_revision_mismatch", (await f.Completed()).ErrorCode);
        Assert.Equal(0, backend.InferenceCalls);
    }

    [Fact]
    public async Task A04_ProbeCancellationPreservesLinkedCallerToken()
    {
        await using var f = new SceneFixture(); var backend = await Wrap(f);
        using var source = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(source.Token, TestContext.Current.CancellationToken);
        backend.Probe = _ => { source.Cancel(); throw new TaskCanceledException(Secret, new IOException(Secret)); };
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await f.Service.ProbeAsync(linked.Token));
        Assert.Equal(linked.Token, error.CancellationToken); Assert.DoesNotContain(Secret, error.ToString());
        Assert.Null(error.InnerException); Assert.Empty(error.Data); Assert.Equal(0, backend.InferenceCalls);
    }

    [Theory]
    [InlineData("cancel", "canceled", "caller_canceled")]
    [InlineData("deadline", "timed_out", "observation_deadline")]
    [InlineData("invalidate", "superseded", "context_invalidated")]
    public async Task A04_LateUnknownFailureCannotReplaceAnEarlierTerminalResult(string operation, string state, string reason)
    {
        await using var f = new SceneFixture(); var request = await f.Request(); f.Backend.Block = true;
        await f.Submit(request); await f.WaitForCall();
        if (operation == "deadline") f.Clock.Advance(3100);
        else await f.Invoke(operation == "cancel" ? "scene.cancel" : "scene.invalidate", operation == "cancel"
            ? new Dictionary<string, string> { ["requestId"] = request.RequestId } : null);
        f.Backend.FailAll(new SceneException(Secret, workMayStillBeRunning: true)); await Idle(f);
        var result = await f.Status(); Assert.Equal(state, result.Status); Assert.Equal(reason, result.ErrorCode);
        Assert.Empty(result.Proposals); await AssertPhysical(f, true); await AssertJournalSafe(f);
    }

    [Fact]
    public async Task A10_LegacyCompletedUnknownReasonRevokesCandidatesButKeepsAnalysisHistory()
    {
        await using var f = new SceneFixture(); var request = await f.Request(); await f.Submit(request);
        var completed = await f.Completed(); Assert.Single(completed.Proposals); await Idle(f);
        await f.Service.DisposeAsync(); var path = Path.Combine(f.Directory, "jobs.json");
        var journal = SceneJson.Read<SceneJournalDocument>(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken), 1024 * 1024);
        var entry = Assert.Single(journal.Entries);
        await File.WriteAllTextAsync(path, SceneJson.Write(journal with { Entries = [entry with { Result = entry.Result with { ErrorCode = Secret } }] }),
            TestContext.Current.CancellationToken);
        f.Service = new(f.Options, f.Backend, f, f.Clock); f.Session = f.Service.OpenSession(f.Principal());
        var result = await f.Status(); Assert.Equal("completed", result.Status); Assert.Equal("invalid_backend_response", result.ErrorCode);
        Assert.Empty(result.Proposals); Assert.False(result.FreshnessVerified);
        Assert.Equal(SceneJson.Write(completed.Observations), SceneJson.Write(result.Observations)); await AssertJournalSafe(f);
        await f.Restart(); Assert.Empty((await f.Status()).Proposals); Assert.Equal(1, f.Backend.Count);
    }

    [Fact]
    public async Task A01_KnownPersistedErrorBytesAndHashAreUnchanged()
    {
        await using var f = new SceneFixture(); f.Backend.Failure = new("backend_busy");
        await f.Submit(await f.Request()); await f.Completed(); await Idle(f);
        var path = Path.Combine(f.Directory, "jobs.json");
        var original = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        await f.Restart(); var restored = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(original, restored); Assert.Equal(SceneJson.Hash(original), SceneJson.Hash(restored));
    }

    private static async Task AssertPhysical(SceneFixture f, bool outstanding)
    {
        var journal = SceneJson.Read<SceneJournalDocument>(await File.ReadAllTextAsync(Path.Combine(f.Directory, "jobs.json"),
            TestContext.Current.CancellationToken), 1024 * 1024);
        Assert.Equal(outstanding, Assert.Single(journal.Entries).PhysicalOutstanding);
        var readiness = (await Catalog(f)).Readiness;
        if (outstanding) { Assert.Equal("degraded", readiness.State); Assert.Equal("backend_work_unconfirmed", readiness.ReasonCode); }
        else Assert.NotEqual("backend_work_unconfirmed", readiness.ReasonCode);
    }

    private static string Code(string kind) => kind switch
    {
        "custom" => Secret,
        "prefix" => "backend_busy:" + Secret,
        "case" => "BACKEND_BUSY",
        "whitespace" => "backend_busy ",
        "newline" => "backend_busy\n" + Secret,
        "empty" => "", "null" => null!, "long" => Secret + new string('x', 64 * 1024),
        _ => kind
    };
    private static void AssertUnavailable(SceneReadiness result)
    {
        Assert.Equal("unavailable", result.State); Assert.Equal("probe_failed", result.ReasonCode);
        Assert.False(result.DeploymentVerified); Assert.Null(result.RetryAfterMs);
        Assert.DoesNotContain(Secret, SceneJson.Write(result));
    }
    private static async Task<SceneCapabilityCatalog> Catalog(SceneFixture f) => SceneJson.Read<SceneCapabilityCatalog>(
        (await f.Invoke("scene.capabilities")).Metadata[SceneService.ResultMetadataKey], 128 * 1024);
    private static Task Idle(SceneFixture f) => SceneFixture.Eventually(async () =>
        (await Catalog(f)).ActiveInference == 0 && (await f.Status()).BackendCancellation != "requested");
    private static async Task AssertJournalSafe(SceneFixture f) => Assert.DoesNotContain(Secret,
        await File.ReadAllTextAsync(Path.Combine(f.Directory, "jobs.json"), TestContext.Current.CancellationToken));
    private static async Task<BoundaryBackend> Wrap(SceneFixture f)
    {
        await f.Service.DisposeAsync(); var backend = new BoundaryBackend(f.Backend);
        f.Service = new(f.Options, backend, f, f.Clock); f.Session = f.Service.OpenSession(f.Principal()); return backend;
    }
    private sealed class BoundaryBackend(FakeBackend inner) : ISceneReasonerBackend
    {
        internal Func<CancellationToken, ValueTask<SceneReadiness>>? Probe;
        internal Exception? UnexpectedFailure;
        internal int InferenceCalls;
        public SceneBackendIdentity Identity => inner.Identity;
        public ValueTask<SceneReadiness> ProbeAsync(CancellationToken token) => Probe is null ? inner.ProbeAsync(token) : Probe(token);
        public Task<SceneBackendReply> AnalyzeAsync(SceneBackendInput input, CancellationToken token)
        { Interlocked.Increment(ref InferenceCalls); return UnexpectedFailure is null ? inner.AnalyzeAsync(input, token) : Task.FromException<SceneBackendReply>(UnexpectedFailure); }
        public ValueTask<string> RequestCancellationAsync(string id, CancellationToken token) => inner.RequestCancellationAsync(id, token);
    }
}
