using System.Net;

namespace AIKernel.Providers.Cosmos.Tests;

public sealed class SceneHostErrorTests
{
    private const string Secret = "private-host-token-DO-NOT-RETURN";

    [Theory]
    [InlineData("custom")]
    [InlineData("prefix")]
    [InlineData("case")]
    [InlineData("space")]
    [InlineData("empty")]
    [InlineData("null")]
    [InlineData("long")]
    [InlineData("generic")]
    [InlineData("io")]
    [InlineData("known")]
    [InlineData("cancel")]
    [InlineData("task")]
    public async Task Host_QuiescenceErrorsAreSanitizedAndDoNotReleaseQuarantine(string kind)
    {
        await using var f = new SceneFixture(); var backend = new HostBackend(f.Backend);
        await Replace(f, backend);
        f.Backend.Failure = new("backend_transport_unconfirmed", true);
        await f.Submit(await f.Request()); await f.Completed(); await Idle(f);
        backend.Failure = Error(kind);
        var error = Record.Exception(f.Service.ConfirmBackendQuiescent);
        AssertSafe(error, kind, kind == "known" ? "backend_still_active" : "host_operation_failed");
        backend.Failure = null;
        Assert.Equal("backend_work_unconfirmed", (await f.Service.ProbeAsync(TestContext.Current.CancellationToken)).ReasonCode);
        f.Service.ConfirmBackendQuiescent();
        Assert.Equal("ready", (await f.Service.ProbeAsync(TestContext.Current.CancellationToken)).State);
    }

    [Theory]
    [InlineData("custom")]
    [InlineData("generic")]
    [InlineData("known")]
    [InlineData("cancel")]
    [InlineData("task")]
    public async Task Host_RevocationErrorsDoNotExposeClockDetails(string kind)
    {
        await using var f = new SceneFixture(); var clock = new HostClock(f.Clock);
        await Replace(f, f.Backend, clock);
        await f.Submit(await f.Request()); await f.Completed(); await Idle(f);
        clock.Failure = Error(kind);
        var error = Record.Exception(() => f.Service.RevokeRequestAuthorization("caller-a", "req-1"));
        clock.Failure = null;
        AssertSafe(error, kind, kind == "known" ? "backend_still_active" : "host_operation_failed");
        f.Service.RevokeRequestAuthorization("caller-a", "req-1");
        Assert.Empty((await f.Status()).Proposals);
    }

    [Theory]
    [InlineData("generic")]
    [InlineData("cancel")]
    [InlineData("task")]
    public async Task Host_ShutdownErrorsDoNotExposeTimerDetails(string kind)
    {
        await using var f = new SceneFixture(); var clock = new HostClock(f.Clock);
        await Replace(f, f.Backend, clock); f.Backend.Block = true;
        await f.Submit(await f.Request()); await f.WaitForCall();
        clock.TimerFailure = Error(kind);
        var error = await Record.ExceptionAsync(async () => await f.Service.DisposeAsync());
        clock.TimerFailure = null;
        AssertSafe(error, kind, "host_operation_failed");
        f.Backend.ReleaseAll();
    }

    [Fact]
    public async Task Host_DeploymentReadDoesNotExposePrivateFilePath()
    {
        await using var f = new SceneFixture();
        var source = new SceneDeploymentFileSource(Path.Combine(f.Directory, Secret + ".json"));
        var error = await Record.ExceptionAsync(async () => await source.ReadAsync(TestContext.Current.CancellationToken));
        AssertSafe(error, "io", "deployment_evidence_invalid");
    }

    [Theory]
    [InlineData("custom")]
    [InlineData("generic")]
    [InlineData("task")]
    public void Host_NimDisposeDoesNotExposeHandlerDetails(string kind)
    {
        var handler = new HostHandler { Failure = Error(kind) };
        var backend = new CosmosNimBackend(Options(), new HostEvidence(), handler);
        var error = Record.Exception(backend.Dispose);
        AssertSafe(error, kind, "host_operation_failed");
        handler.Failure = null; backend.Dispose();
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("task")]
    public async Task Host_DirectNimProbeCancellationDoesNotExposeEvidenceDetails(string kind)
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        using var backend = new CosmosNimBackend(Options(), new HostEvidence { Failure = Error(kind) }, new HostHandler());
        var error = await Record.ExceptionAsync(async () => await backend.ProbeAsync(cancellation.Token));
        AssertSafe(error, kind, "unused", cancellation.Token);
    }

    [Theory]
    [InlineData("custom")]
    [InlineData("generic")]
    [InlineData("known")]
    [InlineData("cancel")]
    [InlineData("task")]
    public async Task InitializationIdentityFailureIsSafeAndDoesNotHoldJournalLease(string kind)
    {
        await using var f = new SceneFixture(); await f.Service.DisposeAsync();
        var backend = new HostBackend(f.Backend) { Failure = Error(kind) };
        var error = Record.Exception(() => new SceneService(f.Options, backend, f, f.Clock));
        backend.Failure = null;
        f.Service = new(f.Options, backend, f, f.Clock); f.Session = f.Service.OpenSession(f.Principal());
        AssertSafe(error, kind, kind == "known" ? "backend_still_active" : "host_operation_failed");
        Assert.Equal("ready", (await f.Service.ProbeAsync(TestContext.Current.CancellationToken)).State);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("schema")]
    public async Task InvalidJournalRemainsRejectedAndInitializationReleasesLease(string kind)
    {
        await using var f = new SceneFixture(); await f.Service.DisposeAsync();
        var path = Path.Combine(f.Directory, "jobs.json");
        var original = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(path, kind == "malformed" ? Secret : "{\"schemaVersion\":\"private\",\"entries\":[]}", TestContext.Current.CancellationToken);
        var error = Record.Exception(() => new SceneService(f.Options, f.Backend, f, f.Clock));
        AssertSafe(error, "standard", "journal_invalid");
        await File.WriteAllTextAsync(path, original, TestContext.Current.CancellationToken);
        f.Service = new(f.Options, f.Backend, f, f.Clock);
    }

    [Theory]
    [InlineData("file")]
    [InlineData("writer")]
    public async Task JournalInitializationDoesNotExposePathsOrWriterDetails(string kind)
    {
        await using var f = new SceneFixture();
        var path = Path.Combine(f.Directory, Secret);
        await File.WriteAllTextAsync(path, "owned test file", TestContext.Current.CancellationToken);
        var options = kind == "writer" ? f.Options : new SceneOptions { JournalDirectory = path };
        var error = Record.Exception(() => new SceneService(options, f.Backend, f, f.Clock));
        AssertSafe(error, "standard", "journal_unavailable");
        Assert.Equal("ready", (await f.Service.ProbeAsync(TestContext.Current.CancellationToken)).State);
    }

    [Fact]
    public async Task FailedRecoveryJournalWriteDoesNotReleaseQuarantine()
    {
        await using var f = new SceneFixture(); f.Backend.Failure = new("backend_transport_unconfirmed", true);
        await f.Submit(await f.Request()); await f.Completed(); await Idle(f);
        var directory = Path.GetFullPath(f.Directory);
        var journal = Path.Combine(directory, "jobs.json"); var backup = Path.Combine(directory, "recovery-test-backup.json");
        Assert.Equal(directory, Path.GetDirectoryName(journal)); Assert.Equal(directory, Path.GetDirectoryName(backup));
        // No active writer remains. A directory obstruction fails on every OS.
        File.Move(journal, backup); Directory.CreateDirectory(journal);
        try { AssertSafe(Record.Exception(f.Service.ConfirmBackendQuiescent), "standard", "journal_unavailable"); }
        finally { Directory.Delete(journal); File.Move(backup, journal); }
        Assert.Equal("backend_work_unconfirmed", (await f.Service.ProbeAsync(TestContext.Current.CancellationToken)).ReasonCode);
        await f.Restart();
        Assert.Equal("backend_work_unconfirmed", (await f.Service.ProbeAsync(TestContext.Current.CancellationToken)).ReasonCode);
        f.Service.ConfirmBackendQuiescent();
        Assert.Equal("ready", (await f.Service.ProbeAsync(TestContext.Current.CancellationToken)).State);
    }

    [Theory]
    [InlineData("invalid", "invalid_identifier")]
    [InlineData("missing", "request_not_found")]
    [InlineData("disposed", "disabled")]
    public async Task StandardHostErrorsKeepTheirCodes(string kind, string expected)
    {
        await using var f = new SceneFixture();
        if (kind == "disposed") await f.Service.DisposeAsync();
        AssertSafe(Record.Exception(() => f.Service.RevokeRequestAuthorization(kind == "invalid" ? "invalid caller" : "caller-a", "missing")),
            "standard", expected);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("duplicate")]
    [InlineData("large")]
    public async Task InvalidDeploymentEvidenceIsRejectedWithoutItsContents(string kind)
    {
        await using var f = new SceneFixture(); var path = Path.Combine(f.Directory, Secret + ".json");
        await File.WriteAllTextAsync(path, kind switch
        { "duplicate" => "{\"model\":\"" + Secret + "\",\"model\":\"other\"}", "large" => new string('x', 8193), _ => Secret }, TestContext.Current.CancellationToken);
        var source = new SceneDeploymentFileSource(path);
        AssertSafe(await Record.ExceptionAsync(async () => await source.ReadAsync(TestContext.Current.CancellationToken)), "standard", "deployment_evidence_invalid");
    }

    [Fact]
    public async Task DeploymentEvidenceSuccessAndCallerCancellationArePreserved()
    {
        await using var f = new SceneFixture(); var path = Path.Combine(f.Directory, "evidence.json");
        var expected = new SceneDeploymentEvidence("model", "revision", "fp8", "nim", ["GPU-test"], "sha256:image", f.Clock.GetUtcNow(), f.Clock.GetUtcNow().AddMinutes(1), "ready");
        await File.WriteAllTextAsync(path, SceneJson.Write(expected), TestContext.Current.CancellationToken);
        var source = new SceneDeploymentFileSource(path);
        Assert.Equal(SceneJson.Write(expected), SceneJson.Write(await source.ReadAsync(TestContext.Current.CancellationToken)));
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); caller.Cancel();
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await source.ReadAsync(caller.Token));
        Assert.Equal(caller.Token, error.CancellationToken); Assert.Null(error.InnerException); Assert.Empty(error.Data);
    }

    [Theory]
    [InlineData("relative")]
    [InlineData("nul")]
    public void DeploymentConfigurationErrorsHaveFixedMessages(string kind)
    {
        var path = kind == "relative" ? Secret : Path.Combine(Path.GetTempPath(), Secret + "\0");
        var error = Assert.Throws<ArgumentException>(() => new SceneDeploymentFileSource(path));
        Assert.DoesNotContain(Secret, error.ToString()); Assert.Equal("path", error.ParamName);
        Assert.Null(error.InnerException); Assert.Empty(error.Data);
    }

    [Theory]
    [InlineData("custom")]
    [InlineData("generic")]
    [InlineData("task")]
    public void NimConfigurationErrorsCannotEchoCustomCollections(string kind)
    {
        var options = Options(new FaultingAddresses(Error(kind)));
        var error = Record.Exception(() => new CosmosNimBackend(options, new HostEvidence()));
        if (kind == "task") AssertSafe(error, kind, "unused");
        else
        {
            var argument = Assert.IsType<ArgumentException>(error);
            Assert.Equal("options", argument.ParamName); Assert.Null(error.InnerException); Assert.Empty(error.Data);
            Assert.DoesNotContain(Secret, error.ToString());
        }
    }

    private static Exception Error(string kind)
    {
        Exception error = kind switch
        {
            "generic" => new InvalidOperationException(Secret, new IOException(Secret)),
            "io" => new IOException(Secret, new InvalidOperationException(Secret)),
            "cancel" => new OperationCanceledException(Secret, new IOException(Secret), new CancellationToken(true)),
            "task" => new TaskCanceledException(Secret, new IOException(Secret), new CancellationToken(true)),
            _ => new SceneException(kind switch
            {
                "prefix" => "backend_still_active:" + Secret, "case" => "BACKEND_STILL_ACTIVE",
                "space" => "backend_still_active ", "empty" => "", "null" => null!,
                "long" => Secret + new string('x', 65536), "known" => "backend_still_active", _ => Secret
            }, workMayStillBeRunning: true)
        };
        error.Data["private"] = Secret; return error;
    }
    private static void AssertSafe(Exception? error, string kind, string code, CancellationToken? expectedToken = null)
    {
        Assert.NotNull(error); Assert.Null(error.InnerException); Assert.Empty(error.Data);
        Assert.DoesNotContain(Secret, error.ToString());
        if (kind is "task" or "cancel")
        {
            if (kind == "task") Assert.IsType<TaskCanceledException>(error); else Assert.IsType<OperationCanceledException>(error);
            Assert.Equal(expectedToken ?? CancellationToken.None, ((OperationCanceledException)error).CancellationToken);
            if (error is TaskCanceledException task) Assert.Null(task.Task);
        }
        else
        {
            var scene = Assert.IsType<SceneException>(error);
            Assert.Equal(code, scene.Code); Assert.Equal(code, scene.Message);
            Assert.Equal(kind is "custom" or "prefix" or "case" or "space" or "empty" or "null" or "long" or "known", scene.WorkMayStillBeRunning);
        }
    }
    private static async Task Replace(SceneFixture f, ISceneReasonerBackend backend, TimeProvider? clock = null)
    {
        await f.Service.DisposeAsync(); f.Service = new(f.Options, backend, f, clock ?? f.Clock);
        f.Session = f.Service.OpenSession(f.Principal());
    }
    private static Task Idle(SceneFixture f) => SceneFixture.Eventually(async () =>
        SceneJson.Read<SceneCapabilityCatalog>((await f.Invoke("scene.capabilities")).Metadata[SceneService.ResultMetadataKey], 128 * 1024).ActiveInference == 0
        && (await f.Status()).BackendCancellation != "requested");

    private sealed class HostBackend(FakeBackend inner) : ISceneReasonerBackend
    {
        internal Exception? Failure;
        public SceneBackendIdentity Identity => Failure is null ? inner.Identity : throw Failure;
        public ValueTask<SceneReadiness> ProbeAsync(CancellationToken token) => inner.ProbeAsync(token);
        public Task<SceneBackendReply> AnalyzeAsync(SceneBackendInput input, CancellationToken token) => inner.AnalyzeAsync(input, token);
        public ValueTask<string> RequestCancellationAsync(string id, CancellationToken token) => inner.RequestCancellationAsync(id, token);
    }
    private sealed class HostClock(ManualClock inner) : TimeProvider
    {
        internal Exception? Failure;
        internal Exception? TimerFailure;
        public override long TimestampFrequency => inner.TimestampFrequency;
        public override long GetTimestamp() => inner.GetTimestamp();
        public override DateTimeOffset GetUtcNow() => Failure is null ? inner.GetUtcNow() : throw Failure;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan due, TimeSpan period)
            => new HostTimer(this, inner.CreateTimer(callback, state, due, period));
        private sealed class HostTimer(HostClock owner, ITimer inner) : ITimer
        {
            public bool Change(TimeSpan due, TimeSpan period) => inner.Change(due, period);
            public void Dispose() { if (owner.TimerFailure is { } failure) throw failure; inner.Dispose(); }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
    private sealed class HostEvidence : ISceneDeploymentEvidenceSource
    {
        internal Exception? Failure;
        public ValueTask<SceneDeploymentEvidence> ReadAsync(CancellationToken token) => Failure is { } failure
            ? ValueTask.FromException<SceneDeploymentEvidence>(failure) : throw new InvalidOperationException("Probe not expected.");
    }
    private sealed class HostHandler : HttpMessageHandler
    {
        internal Exception? Failure;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            => throw new InvalidOperationException("Network not expected.");
        protected override void Dispose(bool disposing) { if (Failure is { } failure) throw failure; base.Dispose(disposing); }
    }
    private sealed class FaultingAddresses(Exception error) : IReadOnlyList<IPAddress>
    {
        public int Count => throw error;
        public IPAddress this[int index] => throw error;
        public IEnumerator<IPAddress> GetEnumerator() => throw error;
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private static CosmosNimOptions Options(IReadOnlyList<IPAddress>? addresses = null) => new()
    {
        Endpoint = new("https://cosmos.invalid/"), EndpointId = "test-endpoint", Model = "model", Revision = "revision",
        Precision = "fp8", Runtime = "nim", ImageDigest = "sha256:test-image", GpuIds = ["GPU-test"], AllowedAddresses = addresses ?? [IPAddress.Loopback]
    };
}
