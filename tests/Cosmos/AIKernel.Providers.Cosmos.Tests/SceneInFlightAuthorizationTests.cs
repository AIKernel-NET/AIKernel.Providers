using System.Security.Claims;

namespace AIKernel.Providers.Cosmos.Tests;

public sealed class SceneInFlightAuthorizationTests
{
    [Theory]
    [InlineData("actor", "scene.status", "rejected", "actor_revision_mismatch")]
    [InlineData("actor-removed", "scene.status", "rejected", "actor_revision_mismatch")]
    [InlineData("action", "scene.status", "rejected", "action_not_authorized")]
    [InlineData("target", "scene.status", "rejected", "target_not_authorized")]
    [InlineData("scope", "scene.status", "rejected", "permission_denied")]
    [InlineData("policy", "scene.status", "rejected", "permission_denied")]
    [InlineData("denied", "scene.status", "rejected", "permission_denied")]
    [InlineData("null", "scene.status", "rejected", "permission_denied")]
    [InlineData("unauthenticated", "scene.status", "rejected", "unauthenticated")]
    [InlineData("caller", "scene.status", "rejected", "permission_denied")]
    [InlineData("exception", "scene.status", "failed", "authorization_unavailable")]
    [InlineData("canceled-lookup", "scene.status", "failed", "authorization_unavailable")]
    [InlineData("invalid-grant", "scene.status", "failed", "authorization_unavailable")]
    [InlineData("unknown-error", "scene.status", "failed", "authorization_unavailable")]
    [InlineData("actor", "scene.analyze", "rejected", "actor_revision_mismatch")]
    [InlineData("action", "scene.analyze", "rejected", "action_not_authorized")]
    [InlineData("scope", "scene.analyze", "rejected", "permission_denied")]
    [InlineData("denied", "scene.analyze", "rejected", "permission_denied")]
    [InlineData("action", "scene.cancel", "rejected", "action_not_authorized")]
    public async Task A05_ObservedRevocationSurvivesRestorationBeforeInferenceCompletes(
        string change, string operation, string state, string reason)
    {
        await using var f = new SceneFixture(); var request = await Start(f);
        f.Authorization = (_, grant) => Change(grant, change);
        async Task Lookup() => _ = operation == "scene.analyze" ? await f.Submit(request) : await f.Invoke(operation, Id(request));
        if (change == "canceled-lookup") await Assert.ThrowsAsync<TaskCanceledException>(Lookup);
        else await Lookup();
        // Restore before releasing inference. The final reauthorization alone
        // would see the original grant and incorrectly publish this result.
        f.Authorization = null;
        var revoked = await f.Status();
        AssertRejected(revoked, state, reason);
        await SceneFixture.Eventually(() => Task.FromResult(f.Backend.CancelCount == 1));
        Assert.Equal(1, (await Catalog(f)).ActiveInference);
        var retry = await f.Submit(request);
        Assert.True(retry.Succeeded, retry.ErrorCode); AssertRejected(SceneFixture.Parse(retry), state, reason);
        var queued = await f.Request("next-authorized");
        Assert.Equal("queued", SceneFixture.Parse(await f.Submit(queued)).Status);
        Assert.Equal(1, f.Backend.Count);
        f.Backend.Block = false; f.Backend.ReleaseAll();
        Assert.Single((await f.Completed("next-authorized")).Proposals);
        await Idle(f);
        AssertRejected(await f.Status(), state, reason);
        Assert.Equal(request.CapturedAtUtc.AddMilliseconds(request.MaxObservationAgeMs), (await f.Status()).ExpiresAtUtc);
        var journal = SceneJson.Read<SceneJournalDocument>(await File.ReadAllTextAsync(Path.Combine(f.Directory, "jobs.json"),
            TestContext.Current.CancellationToken), 1024 * 1024);
        var stored = Assert.Single(journal.Entries, e => e.Result.RequestId == request.RequestId);
        AssertRejected(stored.Result, state, reason); Assert.False(stored.PhysicalOutstanding);
        Assert.DoesNotContain("private-revocation-detail", SceneJson.Write(journal));
        await f.Restart();
        AssertRejected(await f.Status(), state, reason);
        AssertRejected(SceneFixture.Parse(await f.Submit(request)), state, reason);
        Assert.Equal(2, f.Backend.Count);
    }

    [Fact]
    public async Task A05_HostNotificationPreservesUnpolledRevocationAcrossRestorationAndRestart()
    {
        await using var f = new SceneFixture(); var request = await Start(f);
        f.Authorization = (_, _) => throw new SceneException("permission_denied");
        // The trusted host can notify even when the caller cannot authenticate.
        // There is no status/cancel/analyze request during the denied interval.
        f.Service.RevokeRequestAuthorization("caller-a", request.RequestId);
        f.Authorization = null;
        f.Service.RevokeRequestAuthorization("caller-a", request.RequestId);
        AssertRejected(await f.Status());
        await SceneFixture.Eventually(() => Task.FromResult(f.Backend.CancelCount == 1));
        Assert.Equal(1, (await Catalog(f)).ActiveInference);
        f.Backend.Block = false; f.Backend.ReleaseAll(); await Idle(f);
        AssertRejected(await f.Status());
        await f.Restart();
        f.Service.RevokeRequestAuthorization("caller-a", request.RequestId);
        AssertRejected(SceneFixture.Parse(await f.Submit(request)));
        Assert.Equal(1, f.Backend.Count); Assert.Equal(1, f.Backend.CancelCount);
        await f.Submit(await f.Request("new-request"));
        Assert.Single((await f.Completed("new-request")).Proposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A04_RevocationWinsAgainstAnOlderGrantDuringDispatchOrCompletion(bool completion)
    {
        await using var f = new SceneFixture();
        var principal = f.Principal(); ((ClaimsIdentity)principal.Identity!).AddClaim(new("delayed", "yes"));
        var subject = f.Service.OpenSession(principal);
        var request = await f.Request(session: subject);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        int checks = 0;
        f.Authorization = (p, grant) =>
        {
            // First call accepts; second dispatches; third validates completion.
            if (p.HasClaim("delayed", "yes") && Interlocked.Increment(ref checks) == (completion ? 3 : 2))
            {
                entered.TrySetResult();
                Assert.True(release.Wait(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken));
            }
            return grant;
        };
        try
        {
            Assert.True((await f.Submit(request, subject)).Succeeded);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            f.Service.RevokeRequestAuthorization("caller-a", request.RequestId);
            AssertRejected(await f.Status());
            Assert.Equal(1, (await Catalog(f)).ActiveInference);
            release.Set(); await Idle(f);
            AssertRejected(await f.Status());
            Assert.Equal(completion ? 1 : 0, f.Backend.Count);
            Assert.Equal(0, f.Backend.CancelCount);
        }
        finally { release.Set(); }
    }

    [Fact]
    public async Task A04_EarlierConcurrentLookupCannotUndoObservedRevocation()
    {
        await using var f = new SceneFixture(); var request = await Start(f);
        var principal = f.Principal(); ((ClaimsIdentity)principal.Identity!).AddClaim(new("delayed", "yes"));
        var delayed = f.Service.OpenSession(principal);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        f.Authorization = (p, grant) =>
        {
            if (p.HasClaim("delayed", "yes"))
            {
                entered.TrySetResult();
                Assert.True(release.Wait(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken));
                return grant;
            }
            return Change(grant, "action");
        };
        var pending = f.Invoke("scene.status", Id(request), delayed);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            AssertRejected(await f.Status(), "rejected", "action_not_authorized");
            f.Authorization = null; release.Set();
            AssertRejected(SceneFixture.Parse(await pending), "rejected", "action_not_authorized");
            f.Backend.Block = false; f.Backend.ReleaseAll(); await Idle(f);
            AssertRejected(await f.Status(), "rejected", "action_not_authorized");
        }
        finally { release.Set(); await pending; }
    }

    [Fact]
    public async Task A06_QueuedLookupRevocationNeverDispatchesAfterRestoration()
    {
        await using var f = new SceneFixture(); await Start(f);
        var queued = await f.Request("queued"); await f.Submit(queued);
        f.Authorization = (_, grant) => Change(grant, "target");
        AssertRejected(await f.Status(queued.RequestId), "rejected", "target_not_authorized");
        f.Authorization = null;
        f.Backend.Block = false; f.Backend.ReleaseAll(); await Idle(f);
        Assert.Single((await f.Status()).Proposals);
        AssertRejected(await f.Status(queued.RequestId), "rejected", "target_not_authorized");
        Assert.Equal(1, f.Backend.Count); Assert.Equal(0, f.Backend.CancelCount);
    }

    [Fact]
    public async Task A06_NotificationIsBoundToCallerAndExistingRequestAndNotExposedAsCapability()
    {
        await using var f = new SceneFixture(); var request = await Start(f);
        var other = f.Service.OpenSession(f.Principal("caller-b"));
        await f.Submit(await f.Request(session: other), other);
        Assert.Equal("request_not_found", Assert.Throws<SceneException>(() =>
            f.Service.RevokeRequestAuthorization("caller-c", request.RequestId)).Code);
        Assert.Equal("request_not_found", Assert.Throws<SceneException>(() =>
            f.Service.RevokeRequestAuthorization("caller-a", "missing-request")).Code);
        Assert.Throws<SceneException>(() => f.Service.RevokeRequestAuthorization("invalid caller", request.RequestId));
        var unsupported = await f.Invoke("scene.revokeAuthorization", Id(request));
        Assert.False(unsupported.Succeeded); Assert.Equal("operation_unsupported", unsupported.ErrorCode);
        Assert.DoesNotContain("scene.revokeAuthorization", (await Catalog(f)).Operations);
        f.Service.RevokeRequestAuthorization("caller-b", request.RequestId);
        Assert.Equal("running", (await f.Status()).Status);
        AssertRejected(await f.Status(session: other));
        f.Backend.Block = false; f.Backend.ReleaseAll(); await Idle(f);
        Assert.Single((await f.Status()).Proposals); Assert.Equal(1, f.Backend.Count);
    }

    [Fact]
    public async Task A05_HostCanRevokeCompletedCandidatesWithoutRewritingAnalysisHistory()
    {
        await using var f = new SceneFixture(); var request = await f.Request(); await f.Submit(request);
        var original = await f.Completed(); await Idle(f); Assert.Single(original.Proposals);
        f.Service.RevokeRequestAuthorization("caller-a", request.RequestId);
        var revoked = original with { Proposals = [], FreshnessVerified = false, ErrorCode = "stale_context" };
        Assert.Equal(SceneJson.Write(revoked), SceneJson.Write(await f.Status()));
        await f.Restart(); f.Service.RevokeRequestAuthorization("caller-a", request.RequestId);
        Assert.Equal(SceneJson.Write(revoked), SceneJson.Write(await f.Status()));
        Assert.Equal(0, f.Backend.CancelCount); Assert.Equal(1, f.Backend.Count);
    }

    [Theory]
    [InlineData("invalid-capability")]
    [InlineData("invalid-arguments")]
    [InlineData("conflicting-input")]
    [InlineData("caller-canceled")]
    [InlineData("unrelated-permissions")]
    public async Task A06_InvalidOrUnrelatedLookupDoesNotRevokeAnInFlightRequest(string mode)
    {
        await using var f = new SceneFixture(); var request = await Start(f);
        f.Authorization = (_, grant) => mode == "unrelated-permissions" ? grant with
        {
            ActorRevisions = new Dictionary<string, long> { ["actor-a"] = 12 },
            ActionIds = new HashSet<string> { "look_at" }, TargetIds = new HashSet<string> { "board-a" }
        } : throw new SceneException("permission_denied");
        if (mode == "conflicting-input") await f.Submit(request with { MaxProposals = 0 });
        else if (mode == "unrelated-permissions") Assert.Equal("running", (await f.Status()).Status);
        else
        {
            using var cancel = new CancellationTokenSource(); if (mode == "caller-canceled") cancel.Cancel();
            async Task Lookup() => _ = await f.Session.InvokeAsync(new("inv-test",
                mode == "invalid-capability" ? "other.capability" : SceneService.CapabilityId, "scene.status",
                mode == "invalid-arguments" ? new Dictionary<string, string> { ["requestId"] = request.RequestId, ["extra"] = "ignored" } : Id(request),
                null, null, new Dictionary<string, string>()), cancel.Token);
            if (mode == "caller-canceled") await Assert.ThrowsAnyAsync<OperationCanceledException>(Lookup); else await Lookup();
        }
        f.Authorization = null;
        Assert.Equal("running", (await f.Status()).Status);
        f.Backend.Block = false; f.Backend.ReleaseAll();
        Assert.Single((await f.Completed()).Proposals); Assert.Equal(0, f.Backend.CancelCount);
    }

    [Theory]
    [InlineData("cancel", "canceled")]
    [InlineData("deadline", "timed_out")]
    [InlineData("invalidate", "superseded")]
    public async Task A04_RevocationDoesNotOverwriteEarlierTerminalState(string operation, string state)
    {
        await using var f = new SceneFixture(); var request = await Start(f);
        if (operation == "deadline") f.Clock.Advance(3100);
        else await f.Invoke(operation == "cancel" ? "scene.cancel" : "scene.invalidate", operation == "cancel" ? Id(request) : null);
        await SceneFixture.Eventually(() => Task.FromResult(f.Backend.CancelCount == 1));
        f.Service.RevokeRequestAuthorization("caller-a", request.RequestId);
        Assert.Equal(state, (await f.Status()).Status);
        f.Backend.Block = false; f.Backend.ReleaseAll(); await Idle(f);
        Assert.Equal(state, (await f.Status()).Status); Assert.Empty((await f.Status()).Proposals);
        Assert.Equal(1, f.Backend.CancelCount);
    }

    [Fact]
    public async Task A10_FailedRevocationWriteIsNotAcknowledgedAndCannotPublishLateOutput()
    {
        await using var f = new SceneFixture(); var request = await Start(f);
        var directory = Path.GetFullPath(f.Directory);
        var journal = Path.Combine(directory, "jobs.json"); var backup = Path.Combine(directory, "revocation-test-backup.json");
        Assert.Equal(directory, Path.GetDirectoryName(journal)); Assert.Equal(directory, Path.GetDirectoryName(backup));
        // Inference is blocked: no job writer can race this obstruction setup.
        File.Move(journal, backup); Directory.CreateDirectory(journal);
        try
        {
            for (int attempt = 0; attempt < 2; attempt++)
                Assert.Equal("journal_unavailable", Assert.Throws<SceneException>(() =>
                    f.Service.RevokeRequestAuthorization("caller-a", request.RequestId)).Code);
            AssertRejected(await f.Status());
            var blocked = await f.Submit(request with { RequestId = "new-blocked" });
            Assert.False(blocked.Succeeded); Assert.Equal("backend_work_unconfirmed", blocked.ErrorCode);
            f.Backend.Block = false; f.Backend.ReleaseAll(); await Idle(f);
            AssertRejected(await f.Status()); Assert.Equal(1, f.Backend.Count);
        }
        finally { Directory.Delete(journal); File.Move(backup, journal); }
        f.Service.RevokeRequestAuthorization("caller-a", request.RequestId);
        await f.Restart(); AssertRejected(await f.Status());
    }

    private static void AssertRejected(SceneAnalysisResult result, string state = "rejected", string reason = "permission_denied")
    {
        Assert.Equal(state, result.Status); Assert.Equal(reason, result.ErrorCode);
        Assert.Empty(result.Observations); Assert.Empty(result.Proposals); Assert.False(result.FreshnessVerified);
    }
    private static async Task<SceneRequest> Start(SceneFixture f)
    {
        var request = await f.Request(); f.Backend.Block = true;
        Assert.True((await f.Submit(request)).Succeeded); await f.WaitForCall(); return request;
    }
    private static Dictionary<string, string> Id(SceneRequest request) => new() { ["requestId"] = request.RequestId };
    private static async Task<SceneCapabilityCatalog> Catalog(SceneFixture f) => SceneJson.Read<SceneCapabilityCatalog>(
        (await f.Invoke("scene.capabilities")).Metadata[SceneService.ResultMetadataKey], 128 * 1024);
    private static Task Idle(SceneFixture f) => SceneFixture.Eventually(async () => (await Catalog(f)).ActiveInference == 0);
    private static SceneGrant Change(SceneGrant grant, string change) => change switch
    {
        "actor" => grant with { ActorRevisions = new Dictionary<string, long> { ["actor-a"] = 13 } },
        "actor-removed" => grant with { ActorRevisions = new Dictionary<string, long>() },
        "action" => grant with { ActionIds = new HashSet<string>() },
        "target" => grant with { TargetIds = new HashSet<string>() },
        "scope" => grant with { PermissionScope = "other-scope" },
        "policy" => grant with { PolicyRevision = "policy-new" },
        "denied" => throw new SceneException("permission_denied"),
        "null" => null!,
        "unauthenticated" => throw new SceneException("unauthenticated"),
        "caller" => grant with { CallerId = "caller-other" },
        "exception" => throw new InvalidOperationException("private-revocation-detail"),
        "canceled-lookup" => throw new TaskCanceledException("private-revocation-detail"),
        "invalid-grant" => grant with { CallerId = "invalid caller" },
        "unknown-error" => throw new SceneException("private-revocation-detail", workMayStillBeRunning: true),
        _ => throw new InvalidOperationException(change)
    };
}
