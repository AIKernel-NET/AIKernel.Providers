using System.Security.Claims;

namespace AIKernel.Providers.Cosmos.Tests;

public sealed class SceneDispatchAuthorizationTests
{
    private const string CredentialClaim = "test-credential";
    private const string Secret = "private-credential-never-persist";

    [Theory]
    [InlineData("denied", "rejected", "permission_denied")]
    [InlineData("null", "rejected", "permission_denied")]
    [InlineData("unauthenticated", "rejected", "unauthenticated")]
    [InlineData("caller", "rejected", "permission_denied")]
    [InlineData("scope", "rejected", "permission_denied")]
    [InlineData("policy", "rejected", "permission_denied")]
    [InlineData("actor", "rejected", "actor_revision_mismatch")]
    [InlineData("action", "rejected", "action_not_authorized")]
    [InlineData("target", "rejected", "target_not_authorized")]
    [InlineData("exception", "failed", "authorization_unavailable")]
    [InlineData("canceled-lookup", "failed", "authorization_unavailable")]
    [InlineData("invalid-grant", "failed", "authorization_unavailable")]
    [InlineData("unknown-error", "failed", "authorization_unavailable")]
    public async Task A06_QueuedJobUsesCurrentAuthorityAndNeverDispatchesAfterRevocation(
        string change, string state, string reason)
    {
        await using var f = new SceneFixture();
        var (subject, monitor, request) = await QueueSubject(f);
        f.Authorization = (principal, grant) => principal.HasClaim(CredentialClaim, Secret) ? change switch
        {
            "denied" => throw new SceneException("permission_denied"),
            "null" => null!,
            "unauthenticated" => throw new SceneException("unauthenticated"),
            "caller" => grant with { CallerId = "caller-other" },
            "scope" => grant with { PermissionScope = "private" },
            "policy" => grant with { PolicyRevision = "public-policy-4" },
            "actor" => grant with { ActorRevisions = new Dictionary<string, long> { ["actor-a"] = 13 } },
            "action" => grant with { ActionIds = new HashSet<string>() },
            "target" => grant with { TargetIds = new HashSet<string>() },
            "exception" => throw new InvalidOperationException(Secret),
            "canceled-lookup" => throw new TaskCanceledException(Secret),
            "invalid-grant" => grant with { CallerId = "not a valid caller" },
            "unknown-error" => throw new SceneException(Secret, workMayStillBeRunning: true),
            _ => throw new InvalidOperationException()
        } : grant;
        f.Backend.Block = false; f.Backend.ReleaseAll();
        var result = await Terminal(f, monitor);
        Assert.Equal(state, result.Status); Assert.Equal(reason, result.ErrorCode);
        Assert.Empty(result.Proposals); Assert.False(result.FreshnessVerified);
        Assert.Equal("not_requested", result.BackendCancellation);
        Assert.Equal(1, f.Backend.Count); Assert.Equal(0, f.Backend.CancelCount);
        Assert.DoesNotContain(Secret, SceneJson.Write(result));

        // Restoring the grant does not retry an already rejected idempotency key.
        f.Authorization = null;
        Assert.Equal(state, SceneFixture.Parse(await f.Submit(request, subject)).Status);
        Assert.Equal(1, f.Backend.Count);
        Assert.True((await f.Submit(await f.Request("authorized-next", subject), subject)).Succeeded);
        Assert.Equal("completed", (await Terminal(f, monitor, "authorized-next")).Status);
        Assert.Equal(2, f.Backend.Count); // A failed authorization lookup did not quarantine the GPU.
        await f.Restart();
        monitor = f.Service.OpenSession(f.Principal("caller-b"));
        Assert.Equal(state, (await f.Status(request.RequestId, monitor)).Status);
        Assert.Equal(state, SceneFixture.Parse(await f.Submit(request, monitor)).Status);
        Assert.Equal(2, f.Backend.Count);
        Assert.DoesNotContain(Secret, await File.ReadAllTextAsync(Path.Combine(f.Directory, "jobs.json"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A06_UnchangedOrExpandedGrantDispatchesUsingOriginalPrincipal()
    {
        await using var f = new SceneFixture();
        var (_, monitor, request) = await QueueSubject(f);
        int checks = 0;
        f.Authorization = (principal, grant) =>
        {
            if (principal.HasClaim(CredentialClaim, Secret))
            {
                Interlocked.Increment(ref checks);
                Assert.Equal("caller-b", principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                return grant with { ActionIds = new HashSet<string>(grant.ActionIds) { "unused-action" } };
            }
            return grant;
        };
        f.Backend.Block = false; f.Backend.ReleaseAll();
        var result = await Terminal(f, monitor);
        Assert.Equal("completed", result.Status); Assert.Single(result.Proposals);
        Assert.True(result.FreshnessVerified); Assert.Equal(1, checks); Assert.Equal(2, f.Backend.Count);
        Assert.Equal(request.CapturedAtUtc.AddMilliseconds(request.MaxObservationAgeMs), result.ExpiresAtUtc);
        Assert.Equal(SceneJson.Write(request), SceneJson.Write((await f.Backend.Calls.Reader.ReadAsync(TestContext.Current.CancellationToken)).Request));
    }

    [Fact]
    public async Task A06_RevocationDuringReadinessIsCheckedBeforeDispatch()
    {
        await using var f = new SceneFixture();
        var (_, monitor, _) = await QueueSubject(f);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Backend.BeforeProbe = async () => { entered.TrySetResult(); await release.Task; };
        try
        {
            f.Backend.Block = false; f.Backend.ReleaseAll();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            f.Authorization = (principal, grant) => principal.HasClaim(CredentialClaim, Secret)
                ? throw new SceneException("permission_denied") : grant;
            release.TrySetResult();
            Assert.Equal("rejected", (await Terminal(f, monitor)).Status);
            Assert.Equal(1, f.Backend.Count);
        }
        finally { release.TrySetResult(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A04_CancelOrDeadlineDuringAuthorizationPreventsDispatchWithoutReleasingSlotEarly(bool expire)
    {
        await using var f = new SceneFixture();
        var (_, monitor, request) = await QueueSubject(f);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        f.Authorization = (principal, grant) =>
        {
            if (principal.HasClaim(CredentialClaim, Secret))
            {
                entered.TrySetResult();
                Assert.True(release.Wait(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken));
            }
            return grant;
        };
        try
        {
            f.Backend.Block = false; f.Backend.ReleaseAll();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            if (expire) f.Clock.Advance(3100);
            else Assert.True((await f.Invoke("scene.cancel", new Dictionary<string, string> { ["requestId"] = request.RequestId }, monitor)
                .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Succeeded);
            var terminal = await f.Status(request.RequestId, monitor);
            Assert.Equal(expire ? "timed_out" : "canceled", terminal.Status);
            Assert.Empty(terminal.Proposals); Assert.Equal("not_requested", terminal.BackendCancellation);
            Assert.Equal(request.CapturedAtUtc.AddMilliseconds(request.MaxObservationAgeMs), terminal.ExpiresAtUtc);
            var next = await f.Request("after-authorization", monitor);
            Assert.Equal("queued", SceneFixture.Parse(await f.Submit(next, monitor)).Status);
            Assert.Equal(1, f.Backend.Count); Assert.Equal(0, f.Backend.CancelCount);
            release.Set();
            Assert.Equal("completed", (await Terminal(f, monitor, next.RequestId)).Status);
            Assert.Equal(2, f.Backend.Count);
            Assert.Equal(terminal.Status, (await f.Status(request.RequestId, monitor)).Status);
        }
        finally { release.Set(); }
    }

    private static async Task<(SceneSession Subject, SceneSession Monitor, SceneRequest Request)> QueueSubject(SceneFixture f)
    {
        f.Backend.Block = true;
        Assert.True((await f.Submit(await f.Request("blocker"))).Succeeded);
        await f.WaitForCall();
        var principal = f.Principal("caller-b");
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(CredentialClaim, Secret));
        var subject = f.Service.OpenSession(principal);
        var monitor = f.Service.OpenSession(f.Principal("caller-b"));
        var request = await f.Request("subject", subject);
        Assert.Equal("queued", SceneFixture.Parse(await f.Submit(request, subject)).Status);
        return (subject, monitor, request);
    }

    private static async Task<SceneAnalysisResult> Terminal(SceneFixture f, SceneSession monitor, string id = "subject")
    {
        SceneAnalysisResult? result = null;
        await SceneFixture.Eventually(async () => { result = await f.Status(id, monitor); return result.Status is not ("queued" or "running"); });
        return result!;
    }
}
