using System.Collections;
using System.Security.Claims;
using AIKernel.Dtos.Capabilities;

namespace AIKernel.Providers.Cosmos.Tests;

public sealed class SceneIntakeErrorTests
{
    private const string Secret = "private-token-DO-NOT-RETURN";

    [Theory]
    [InlineData("custom")]
    [InlineData("prefixed")]
    [InlineData("case")]
    [InlineData("whitespace")]
    [InlineData("newline")]
    [InlineData("empty")]
    [InlineData("null")]
    [InlineData("long")]
    [InlineData("backend-code")]
    [InlineData("generic")]
    public async Task A10_UnknownAuthorizationErrorsAreBoundedAndDoNotEchoSecrets(string kind)
    {
        await using var f = new SceneFixture(); var request = await f.Request();
        f.Authorization = (_, _) => throw Error(kind);
        var result = await f.Submit(request);
        AssertFailure(result, "authorization_unavailable");
        Assert.Equal("existing-replay-hash", result.ReplayLogHash);
        Assert.Equal(0, f.Backend.Count);
    }

    [Theory]
    [InlineData("scene.status")]
    [InlineData("scene.cancel")]
    [InlineData("scene.analyze")]
    [InlineData("scene.invalidate")]
    [InlineData("scene.capabilities")]
    public async Task A10_AllCapabilityOperationsSanitizeAuthorizationFailures(string operation)
    {
        await using var f = new SceneFixture(); var request = await f.Request();
        f.Authorization = (_, _) => throw Error("custom");
        var result = operation == "scene.analyze" ? await f.Submit(request) : await f.Invoke(operation,
            operation is "scene.status" or "scene.cancel" ? new Dictionary<string, string> { ["requestId"] = request.RequestId } : null);
        AssertFailure(result, "authorization_unavailable"); Assert.Equal(0, f.Backend.Count);
    }

    [Theory]
    [InlineData("open", "custom")]
    [InlineData("open", "generic")]
    [InlineData("upload", "custom")]
    [InlineData("upload", "generic")]
    public async Task A10_SessionAndMediaAdmissionSanitizeAuthorizationExceptions(string operation, string kind)
    {
        await using var f = new SceneFixture(); f.Authorization = (_, _) => throw Error(kind);
        var error = operation == "open"
            ? Assert.Throws<SceneException>(() => f.Service.OpenSession(f.Principal()))
            : await Assert.ThrowsAsync<SceneException>(() => Upload(f, new MemoryStream(SceneFixture.Png()), TestContext.Current.CancellationToken));
        Assert.Equal("authorization_unavailable", error.Code); AssertClean(error); Assert.False(error.WorkMayStillBeRunning);
        Assert.Equal(0, f.Backend.Count);
    }

    [Theory]
    [InlineData("invoke", false)]
    [InlineData("invoke", true)]
    [InlineData("open", false)]
    [InlineData("open", true)]
    [InlineData("upload", false)]
    [InlineData("upload", true)]
    public async Task A10_AuthorizerCancellationPreservesCancellationWithoutPrivateDetails(string operation, bool task)
    {
        await using var f = new SceneFixture();
        f.Authorization = (_, _) => throw Cancellation(task);
        var error = operation == "open"
            ? Assert.ThrowsAny<OperationCanceledException>(() => f.Service.OpenSession(f.Principal()))
            : await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation == "invoke"
                ? f.Invoke("scene.capabilities") : Upload(f, new MemoryStream(SceneFixture.Png()), TestContext.Current.CancellationToken));
        if (task) Assert.IsType<TaskCanceledException>(error); else Assert.IsType<OperationCanceledException>(error);
        AssertClean(error); Assert.Equal(0, f.Backend.Count);
    }

    [Theory]
    [InlineData("unauthenticated")]
    [InlineData("permission_denied")]
    [InlineData("actor_revision_mismatch")]
    [InlineData("action_not_authorized")]
    [InlineData("target_not_authorized")]
    [InlineData("authorization_unavailable")]
    public async Task A06_KnownAuthorizationCodesRemainUsableWithoutExceptionData(string code)
    {
        await using var f = new SceneFixture();
        f.Authorization = (_, _) =>
        {
            var error = new SceneException(code, workMayStillBeRunning: true);
            error.Data["credential"] = Secret; throw error;
        };
        AssertFailure(await f.Invoke("scene.capabilities"), code);
        var opened = Assert.Throws<SceneException>(() => f.Service.OpenSession(f.Principal()));
        Assert.Equal(code, opened.Code); AssertClean(opened); Assert.False(opened.WorkMayStillBeRunning);
        var upload = await Assert.ThrowsAsync<SceneException>(() => Upload(f, new MemoryStream(SceneFixture.Png()), TestContext.Current.CancellationToken));
        Assert.Equal(code, upload.Code); AssertClean(upload); Assert.False(upload.WorkMayStillBeRunning);
        Assert.Equal(0, f.Backend.Count);
    }

    [Theory]
    [InlineData("custom", false)]
    [InlineData("prefixed", false)]
    [InlineData("case", false)]
    [InlineData("whitespace", false)]
    [InlineData("newline", false)]
    [InlineData("empty", false)]
    [InlineData("null", false)]
    [InlineData("long", false)]
    [InlineData("backend-code", false)]
    [InlineData("generic", false)]
    [InlineData("invalid_json", true)]
    [InlineData("job_capacity", true)]
    [InlineData("journal_unavailable", true)]
    [InlineData("disabled", true)]
    [InlineData("request_id_conflict", true)]
    [InlineData("media_unavailable", true)]
    public async Task A10_FinalEnvelopeBoundaryAllowsOnlyExactIntakeCodes(string code, bool allowed)
    {
        await using var f = new SceneFixture();
        // A failing integration collection reaches the general intake boundary
        // without relying on the authorizer's narrower allowlist.
        var result = await f.Session.InvokeAsync(new("safe-invocation", SceneService.CapabilityId, "scene.status",
            new ThrowingArguments(allowed ? new SceneException(code) : Error(code)), null, "safe-replay", new Dictionary<string, string>()), TestContext.Current.CancellationToken);
        AssertFailure(result, allowed ? code : "invalid_request");
        Assert.Equal("safe-invocation", result.InvocationId); Assert.Equal("safe-replay", result.ReplayLogHash);
        Assert.Equal(0, f.Backend.Count);
    }

    [Theory]
    [InlineData("custom")]
    [InlineData("generic")]
    [InlineData("known")]
    [InlineData("task-cancel")]
    [InlineData("operation-cancel")]
    public async Task A10_UploadStreamExceptionsCannotBypassIntakeSanitization(string kind)
    {
        await using var f = new SceneFixture();
        Exception original = kind switch
        {
            "known" => new SceneException("media_busy", workMayStillBeRunning: true),
            "task-cancel" => Cancellation(true), "operation-cancel" => Cancellation(false), _ => Error(kind)
        };
        original.Data["credential"] = Secret;
        using var stream = new FailingStream(original);
        var error = await Record.ExceptionAsync(() => Upload(f, stream, TestContext.Current.CancellationToken)); Assert.NotNull(error); AssertClean(error);
        if (kind.Contains("cancel", StringComparison.Ordinal)) Assert.IsAssignableFrom<OperationCanceledException>(error);
        else
        {
            var scene = Assert.IsType<SceneException>(error);
            Assert.Equal(kind == "known" ? "media_busy" : "invalid_request", scene.Code);
            Assert.False(scene.WorkMayStillBeRunning);
        }
        Assert.Equal(0, f.Backend.Count);
        // The upload reservation is released on failure.
        var uploaded = await Upload(f, new MemoryStream(SceneFixture.Png()), TestContext.Current.CancellationToken); Assert.NotEmpty(uploaded.MediaId);
    }

    [Theory]
    [InlineData("invoke")]
    [InlineData("upload")]
    public async Task A04_LinkedCallerCancellationKeepsItsTokenWithoutHostSecrets(string operation)
    {
        await using var f = new SceneFixture();
        using var source = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(source.Token);
        f.Authorization = (_, _) => { source.Cancel(); throw Cancellation(true); };
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            if (operation == "upload") await Upload(f, new MemoryStream(SceneFixture.Png()), linked.Token);
            else await f.Session.InvokeAsync(new("safe-invocation", SceneService.CapabilityId, "scene.capabilities",
                new Dictionary<string, string>(), null, null, new Dictionary<string, string>()), linked.Token);
        });
        AssertClean(error); Assert.Equal(linked.Token, error.CancellationToken); Assert.Equal(0, f.Backend.Count);
    }

    [Theory]
    [InlineData("running")]
    [InlineData("queued")]
    [InlineData("completed")]
    public async Task A05_SanitizedFailureStillDurablyRevokesKnownCandidates(string state)
    {
        await using var f = new SceneFixture();
        f.Backend.Block = state != "completed";
        if (state == "queued") { await f.Submit(await f.Request("blocker")); await f.WaitForCall(); }
        var request = await f.Request(); await f.Submit(request);
        if (state == "running") await f.WaitForCall();
        if (state == "completed") Assert.Single((await f.Completed()).Proposals);
        f.Authorization = (_, _) => throw Error("custom");
        AssertFailure(await f.Invoke("scene.status", new Dictionary<string, string> { ["requestId"] = request.RequestId }), "authorization_unavailable");
        f.Authorization = null;
        f.Backend.Block = false; f.Backend.ReleaseAll();
        await SceneFixture.Eventually(async () => SceneJson.Read<SceneCapabilityCatalog>(
            (await f.Invoke("scene.capabilities")).Metadata[SceneService.ResultMetadataKey], 128 * 1024).ActiveInference == 0);
        var result = await f.Status();
        Assert.Equal(state == "completed" ? "completed" : "failed", result.Status);
        Assert.Equal(state == "completed" ? "stale_context" : "authorization_unavailable", result.ErrorCode);
        Assert.Empty(result.Proposals); Assert.False(result.FreshnessVerified);
        Assert.DoesNotContain(Secret, await File.ReadAllTextAsync(Path.Combine(f.Directory, "jobs.json"), TestContext.Current.CancellationToken));
        await f.Restart(); Assert.Empty((await f.Status()).Proposals);
        var retry = await f.Submit(request); Assert.True(retry.Succeeded); Assert.Empty(SceneFixture.Parse(retry).Proposals);
        Assert.Equal(1, f.Backend.Count);
    }

    [Theory]
    [InlineData("bad-id")]
    [InlineData("null-collections")]
    public async Task A10_InvalidAuthorizationGrantsUseTheSafeAuthorizationFallback(string kind)
    {
        await using var f = new SceneFixture();
        f.Authorization = (_, grant) => kind == "bad-id" ? grant with { CallerId = Secret + "/private" }
            : grant with { ActorRevisions = null! };
        AssertFailure(await f.Invoke("scene.capabilities"), "authorization_unavailable");
        Assert.Equal(0, f.Backend.Count);
    }

    private sealed class FailingStream(Exception error) : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => ValueTask.FromException<int>(error);
    }

    private sealed class ThrowingArguments(Exception error) : IReadOnlyDictionary<string, string>
    {
        public int Count => throw error;
        public IEnumerable<string> Keys => throw error;
        public IEnumerable<string> Values => throw error;
        public string this[string key] => throw error;
        public bool ContainsKey(string key) => throw error;
        public bool TryGetValue(string key, out string value) => throw error;
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => throw error;
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static Exception Error(string kind)
    {
        Exception error = kind == "generic" ? new IOException(Secret, new InvalidOperationException(Secret)) : new SceneException(kind switch
        {
            "custom" => Secret,
            "prefixed" => "permission_denied:" + Secret,
            "case" => "PERMISSION_DENIED",
            "whitespace" => "permission_denied ",
            "newline" => "permission_denied\n" + Secret,
            "empty" => "",
            "null" => null!,
            "long" => Secret + new string('x', 64 * 1024),
            "backend-code" => "backend_oom",
            _ => throw new ArgumentException(kind)
        }, workMayStillBeRunning: true);
        error.Data["credential"] = Secret; return error;
    }
    private static OperationCanceledException Cancellation(bool task)
    {
        OperationCanceledException error = task ? new TaskCanceledException(Secret, new IOException(Secret))
            : new OperationCanceledException(Secret, new IOException(Secret));
        error.Data["credential"] = Secret; return error;
    }
    private static void AssertFailure(CapabilityInvocationResult result, string code)
    {
        Assert.False(result.Succeeded); Assert.Equal(code, result.ErrorCode); Assert.Equal(code, result.ErrorMessage);
        Assert.Null(result.OutputHash); Assert.Empty(result.Metadata); Assert.DoesNotContain(Secret, SceneJson.Write(result));
    }
    private static void AssertClean(Exception error)
    {
        Assert.Null(error.InnerException); Assert.Empty(error.Data);
        Assert.DoesNotContain(Secret, error.ToString()); Assert.DoesNotContain(Secret, error.Message);
        Assert.True(error.Message.Length < 128);
    }
    private static Task<SceneMediaReceipt> Upload(SceneFixture f, Stream stream, CancellationToken token)
        => f.Session.RegisterMediaAsync(new(new("epoch-1", "snap-upload", "program-composite", f.Clock.GetUtcNow(), 41, 8, "camera-wide"),
            "frame-1", 0, "image/png", 2, 2, SceneJson.Hash(SceneFixture.Png()), f.Clock.GetUtcNow().AddMinutes(1)), stream, token);
}
