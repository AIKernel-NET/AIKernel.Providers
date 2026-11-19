using System.Security.Claims;

namespace AIKernel.Providers.Cosmos.Tests;

public sealed class SceneCompletionAuthorizationTests
{
    private const string Credential = "completion-credential";
    private const string Secret = "private-completion-credential";

    [Theory]
    [InlineData("denied", "rejected", "permission_denied")]
    [InlineData("null", "rejected", "permission_denied")]
    [InlineData("unauthenticated", "rejected", "unauthenticated")]
    [InlineData("caller", "rejected", "permission_denied")]
    [InlineData("scope", "rejected", "permission_denied")]
    [InlineData("policy", "rejected", "permission_denied")]
    [InlineData("actor", "rejected", "actor_revision_mismatch")]
    [InlineData("actor-removed", "rejected", "actor_revision_mismatch")]
    [InlineData("action", "rejected", "action_not_authorized")]
    [InlineData("target", "rejected", "target_not_authorized")]
    [InlineData("exception", "failed", "authorization_unavailable")]
    [InlineData("canceled-lookup", "failed", "authorization_unavailable")]
    [InlineData("invalid-grant", "failed", "authorization_unavailable")]
    [InlineData("unknown-error", "failed", "authorization_unavailable")]
    public async Task A06_CompletionRechecksAuthorityBeforePublishingAnyResult(string change, string state, string reason)
    {
        await using var f = new SceneFixture(); var (subject, request) = await Start(f);
        f.Authorization = (p, grant) => p.HasClaim(Credential, Secret) ? change switch
        {
            "denied" => throw new SceneException("permission_denied"),
            "null" => null!,
            "unauthenticated" => throw new SceneException("unauthenticated"),
            "caller" => grant with { CallerId = "caller-other" },
            "scope" => grant with { PermissionScope = "private" },
            "policy" => grant with { PolicyRevision = "policy-new" },
            "actor" => grant with { ActorRevisions = new Dictionary<string, long> { ["actor-a"] = 13 } },
            "actor-removed" => grant with { ActorRevisions = new Dictionary<string, long>() },
            "action" => grant with { ActionIds = new HashSet<string>() },
            "target" => grant with { TargetIds = new HashSet<string>() },
            "exception" => throw new InvalidOperationException(Secret),
            "canceled-lookup" => throw new TaskCanceledException(Secret),
            "invalid-grant" => grant with { CallerId = "invalid caller" },
            "unknown-error" => throw new SceneException(Secret, workMayStillBeRunning: true),
            _ => throw new InvalidOperationException()
        } : grant;
        f.Backend.Block = false; f.Backend.ReleaseAll();
        // The monitor retains its grant: only completion of the original
        // submitter's inference can reject this result, not dev4 lookup checks.
        var result = await f.Completed();
        Assert.Equal(state, result.Status); Assert.Equal(reason, result.ErrorCode);
        Assert.Empty(result.Proposals); Assert.Empty(result.Observations); Assert.False(result.FreshnessVerified);
        Assert.Equal("not_requested", result.BackendCancellation); Assert.Equal(0, f.Backend.CancelCount);
        Assert.Equal(request.CapturedAtUtc.AddMilliseconds(request.MaxObservationAgeMs), result.ExpiresAtUtc);
        f.Authorization = null;
        Assert.Equal(state, SceneFixture.Parse(await f.Submit(request, subject)).Status);
        Assert.Equal(1, f.Backend.Count);
        await Idle(f);
        var stored = await Journal(f);
        Assert.Empty(Assert.Single(stored.Entries).Result.Proposals);
        Assert.DoesNotContain(Secret, SceneJson.Write(stored));
        await f.Restart();
        Assert.Equal(state, (await f.Status()).Status);
        Assert.Equal(state, SceneFixture.Parse(await f.Submit(request)).Status);
        Assert.Equal(1, f.Backend.Count);
        await f.Submit(await f.Request("next-authorized"));
        Assert.Single((await f.Completed("next-authorized")).Proposals);
        Assert.Equal(2, f.Backend.Count); // Authorization failure did not quarantine completed backend work.
    }

    [Fact]
    public async Task A06_CompletionUsesOriginalPrincipalAndPreservesValidCandidates()
    {
        await using var f = new SceneFixture(); var (_, request) = await Start(f);
        int checks = 0;
        f.Authorization = (p, grant) =>
        {
            if (p.HasClaim(Credential, Secret))
            {
                Interlocked.Increment(ref checks);
                Assert.Equal("caller-a", p.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                return grant with { ActionIds = new HashSet<string>(grant.ActionIds) { "unrelated-action" } };
            }
            return grant;
        };
        f.Backend.Block = false; f.Backend.ReleaseAll();
        var result = await f.Completed();
        Assert.Equal(1, checks); Assert.Equal("completed", result.Status); Assert.Single(result.Proposals);
        Assert.True(result.FreshnessVerified); Assert.Equal(1, f.Backend.Count);
        Assert.Equal(request.CapturedAtUtc.AddMilliseconds(request.MaxObservationAgeMs), result.ExpiresAtUtc);
        Assert.Equal(SceneJson.Write(result), SceneJson.Write(await f.Status()));
    }

    [Theory]
    [InlineData("cancel", "canceled")]
    [InlineData("deadline", "timed_out")]
    [InlineData("invalidate", "superseded")]
    public async Task A04_TerminalStateDuringCompletionAuthorizationWinsAndSlotRemainsBounded(string operation, string state)
    {
        await using var f = new SceneFixture(); var (_, request) = await Start(f);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        f.Authorization = (p, grant) =>
        {
            if (p.HasClaim(Credential, Secret))
            {
                entered.TrySetResult();
                Assert.True(release.Wait(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken));
                throw new SceneException("permission_denied");
            }
            return grant;
        };
        try
        {
            f.Backend.Block = false; f.Backend.ReleaseAll();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            if (operation == "deadline")
                await Task.Run(() => f.Clock.Advance(3100), TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            else Assert.True((await f.Invoke(operation == "cancel" ? "scene.cancel" : "scene.invalidate",
                operation == "cancel" ? new Dictionary<string, string> { ["requestId"] = request.RequestId } : null)
                .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Succeeded);
            var terminal = await f.Status();
            Assert.Equal(state, terminal.Status); Assert.Empty(terminal.Proposals);
            Assert.Equal("not_requested", terminal.BackendCancellation); Assert.Equal(0, f.Backend.CancelCount);
            Assert.Equal(request.CapturedAtUtc.AddMilliseconds(request.MaxObservationAgeMs), terminal.ExpiresAtUtc);
            var other = f.Service.OpenSession(f.Principal("caller-b"));
            var next = await f.Request("after-completion-authorization", other);
            Assert.Equal("queued", SceneFixture.Parse(await f.Submit(next, other)).Status);
            Assert.Equal(1, f.Backend.Count);
            release.Set();
            await SceneFixture.Eventually(async () => (await f.Status(next.RequestId, other)).Status == "completed");
            Assert.Equal(2, f.Backend.Count);
            Assert.Equal(SceneJson.Write(terminal), SceneJson.Write(await f.Status()));
        }
        finally { release.Set(); }
    }

    [Fact]
    public async Task A04_CanceledInferenceDoesNotReauthorizeItsLateCompletion()
    {
        await using var f = new SceneFixture(); await Start(f);
        Assert.True((await f.Invoke("scene.cancel", new Dictionary<string, string> { ["requestId"] = "req-1" })).Succeeded);
        int checks = 0;
        f.Authorization = (p, grant) => { if (p.HasClaim(Credential, Secret)) Interlocked.Increment(ref checks); return grant; };
        f.Backend.Block = false; f.Backend.ReleaseAll();
        await Idle(f);
        await SceneFixture.Eventually(() => Task.FromResult(f.Backend.CancelCount == 1));
        Assert.Equal(0, checks); Assert.Equal("canceled", (await f.Status()).Status);
        Assert.Empty((await f.Status()).Proposals); Assert.Equal(1, f.Backend.Count);
    }

    private static async Task<(SceneSession Subject, SceneRequest Request)> Start(SceneFixture f)
    {
        var principal = f.Principal(); ((ClaimsIdentity)principal.Identity!).AddClaim(new(Credential, Secret));
        var subject = f.Service.OpenSession(principal);
        var request = await f.Request(session: subject);
        f.Backend.Block = true;
        Assert.True((await f.Submit(request, subject)).Succeeded); await f.WaitForCall();
        return (subject, request);
    }
    private static async Task<SceneJournalDocument> Journal(SceneFixture f) => SceneJson.Read<SceneJournalDocument>(
        await File.ReadAllTextAsync(Path.Combine(f.Directory, "jobs.json"), TestContext.Current.CancellationToken), 1024 * 1024);
    private static Task Idle(SceneFixture f) => SceneFixture.Eventually(async () => SceneJson.Read<SceneCapabilityCatalog>(
        (await f.Invoke("scene.capabilities")).Metadata[SceneService.ResultMetadataKey], 128 * 1024).ActiveInference == 0);
}
