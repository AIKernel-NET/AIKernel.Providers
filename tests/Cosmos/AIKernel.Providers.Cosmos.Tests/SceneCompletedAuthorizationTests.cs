using System.Security.Claims;

namespace AIKernel.Providers.Cosmos.Tests;

public sealed class SceneCompletedAuthorizationTests
{
    [Theory]
    [InlineData("actor")]
    [InlineData("actor-removed")]
    [InlineData("action")]
    [InlineData("target")]
    [InlineData("scope")]
    [InlineData("policy")]
    [InlineData("denied")]
    [InlineData("null")]
    [InlineData("unauthenticated")]
    [InlineData("caller")]
    [InlineData("exception")]
    [InlineData("canceled-lookup")]
    [InlineData("invalid-grant")]
    public async Task A05_RestoringAuthorityCannotReviveCompletedCandidates(string change)
    {
        await using var f = new SceneFixture();
        var request = await f.Request(); await f.Submit(request);
        var original = await f.Completed(); Assert.Single(original.Proposals);
        f.Authorization = (_, grant) => Change(grant, change);
        if (change == "canceled-lookup")
            await Assert.ThrowsAsync<TaskCanceledException>(() => f.Invoke("scene.status", Id(request)));
        else
        {
            var denied = await f.Invoke("scene.status", Id(request));
            if (denied.Succeeded) Assert.Empty(SceneFixture.Parse(denied).Proposals);
            if (change is "scope" or "policy") { Assert.False(denied.Succeeded); Assert.Equal("request_not_found", denied.ErrorCode); }
        }
        f.Authorization = null;
        AssertRevoked(original, await f.Status());
        var retry = await f.Submit(request);
        Assert.True(retry.Succeeded); AssertRevoked(original, SceneFixture.Parse(retry));
        var anotherSession = f.Service.OpenSession(f.Principal());
        AssertRevoked(original, await f.Status(session: anotherSession));
        // Wait for final journal bookkeeping before opening it for inspection;
        // a test reader must not obstruct the worker's atomic replacement.
        await SceneFixture.Eventually(async () => SceneJson.Read<SceneCapabilityCatalog>(
            (await f.Invoke("scene.capabilities")).Metadata[SceneService.ResultMetadataKey], 128 * 1024).ActiveInference == 0);
        var journal = SceneJson.Read<SceneJournalDocument>(await File.ReadAllTextAsync(Path.Combine(f.Directory, "jobs.json"), TestContext.Current.CancellationToken), 1024 * 1024);
        AssertRevoked(original, Assert.Single(journal.Entries).Result);
        Assert.False(journal.Entries[0].PhysicalOutstanding);
        Assert.Equal(1, f.Backend.Count); Assert.Equal(0, f.Backend.CancelCount);
        await f.Restart();
        AssertRevoked(original, await f.Status());
        var restartedRetry = await f.Submit(request);
        Assert.True(restartedRetry.Succeeded, restartedRetry.ErrorCode);
        AssertRevoked(original, SceneFixture.Parse(restartedRetry));
        Assert.Equal(1, f.Backend.Count);
        await f.Submit(await f.Request("new-authorized-observation"));
        Assert.Single((await f.Completed("new-authorized-observation")).Proposals);
        AssertRevoked(original, await f.Status());
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("action")]
    [InlineData("target")]
    [InlineData("scope")]
    [InlineData("policy")]
    [InlineData("denied")]
    public async Task A04_IdempotentResubmissionAlsoRecordsRevocation(string change)
    {
        await using var f = new SceneFixture(); var request = await f.Request();
        await f.Submit(request); var original = await f.Completed();
        f.Authorization = (_, grant) => Change(grant, change);
        Assert.False((await f.Submit(request)).Succeeded);
        f.Authorization = null;
        AssertRevoked(original, SceneFixture.Parse(await f.Submit(request)));
        Assert.Equal(1, f.Backend.Count);
    }

    [Fact]
    public async Task A06_RevocationIsBoundToTheSessionCallerEvenWhenRequestIdsMatch()
    {
        await using var f = new SceneFixture(); var a = await f.Request(); await f.Submit(a);
        var originalA = await f.Completed();
        var other = f.Service.OpenSession(f.Principal("caller-b"));
        var b = await f.Request(session: other); await f.Submit(b, other);
        await SceneFixture.Eventually(async () => (await f.Status(session: other)).Status == "completed");
        var originalB = await f.Status(session: other);
        f.Authorization = (_, grant) => grant.CallerId == "caller-b" ? grant with { CallerId = "caller-a" } : grant;
        Assert.False((await f.Invoke("scene.status", Id(b), other)).Succeeded);
        f.Authorization = null;
        Assert.Equal(SceneJson.Write(originalA), SceneJson.Write(await f.Status()));
        AssertRevoked(originalB, await f.Status(session: other));
    }

    [Fact]
    public async Task A04_UnrelatedPermissionsAndUnchangedGrantPreserveResultBytes()
    {
        await using var f = new SceneFixture(); var request = await f.Request(); await f.Submit(request);
        var original = await f.Completed();
        f.Authorization = (_, grant) => grant with
        {
            ActorRevisions = new Dictionary<string, long> { ["actor-a"] = 12 },
            ActionIds = new HashSet<string> { "look_at" }, TargetIds = new HashSet<string> { "board-a" }
        };
        var status = await f.Invoke("scene.status", Id(request));
        Assert.Equal(SceneJson.Write(original), status.Metadata[SceneService.ResultMetadataKey]);
        Assert.Equal(SceneJson.Hash(SceneJson.Write(original)), status.OutputHash);
        Assert.Equal(SceneJson.Write(original), SceneJson.Write(SceneFixture.Parse(await f.Submit(request))));
        Assert.Equal(1, f.Backend.Count);
    }

    [Theory]
    [InlineData("invalid-capability")]
    [InlineData("invalid-arguments")]
    [InlineData("conflicting-input")]
    public async Task A06_InvalidLookupCannotRevokeAStoredCandidate(string invalid)
    {
        await using var f = new SceneFixture(); var request = await f.Request(); await f.Submit(request);
        var original = await f.Completed();
        f.Authorization = (_, _) => throw new SceneException("permission_denied");
        if (invalid == "conflicting-input") await f.Submit(request with { MaxProposals = 0 });
        else await f.Session.InvokeAsync(new("inv-test", invalid == "invalid-capability" ? "other.capability" : SceneService.CapabilityId,
            "scene.status", invalid == "invalid-arguments" ? new Dictionary<string, string> { ["requestId"] = request.RequestId, ["extra"] = "ignored" } : Id(request),
            null, null, new Dictionary<string, string>()), TestContext.Current.CancellationToken);
        f.Authorization = null;
        Assert.Equal(SceneJson.Write(original), SceneJson.Write(await f.Status()));
    }

    [Fact]
    public async Task A05_EarlierConcurrentGrantCannotRestoreRevokedCandidates()
    {
        await using var f = new SceneFixture(); var request = await f.Request(); await f.Submit(request);
        var original = await f.Completed();
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
            AssertRevoked(original, await f.Status());
            release.Set();
            AssertRevoked(original, SceneFixture.Parse(await pending));
        }
        finally { release.Set(); await pending; }
    }

    [Fact]
    public async Task A11_OneUnauthorizedActorRevokesTheWholeProposalBatchButPreservesObservations()
    {
        await using var f = new SceneFixture(); var request = await f.Request();
        request = request with { Actors = [request.Actors[0], new("actor-b", 7, "listening", true)],
            AllowedActions = [new("look_at", ["actor-a", "actor-b"], ["board-a"])] };
        f.Backend.Output = SceneJson.Write(new SceneModelResult(
            [new("obs-a", "actor-a", "board-a", "attention_target_mismatch", false, ["frame-1"]), new("obs-b", "actor-b", "board-a", "attention_target_mismatch", false, ["frame-1"])],
            [new("actor-a", 12, "look_at", "board-a", ["obs-a"]), new("actor-b", 7, "look_at", "board-a", ["obs-b"])]));
        await f.Submit(request); var original = await f.Completed(); Assert.Equal(2, original.Proposals.Count);
        f.Authorization = (_, grant) => grant with { ActorRevisions = new Dictionary<string, long> { ["actor-a"] = 12 } };
        AssertRevoked(original, await f.Status()); f.Authorization = null;
        AssertRevoked(original, await f.Status()); Assert.Equal(1, f.Backend.Count);
    }

    [Fact]
    public async Task A05_CancelLookupOfCompletedJobAlsoPersistsRevocation()
    {
        await using var f = new SceneFixture(); var request = await f.Request(); await f.Submit(request);
        var original = await f.Completed();
        f.Authorization = (_, grant) => Change(grant, "action");
        AssertRevoked(original, SceneFixture.Parse(await f.Invoke("scene.cancel", Id(request))));
        f.Authorization = null;
        AssertRevoked(original, await f.Status()); Assert.Equal(0, f.Backend.CancelCount);
    }

    [Fact]
    public async Task A10_JournalFailureDoesNotAcknowledgeRevocationOrReturnCandidates()
    {
        await using var f = new SceneFixture(); var request = await f.Request(); await f.Submit(request);
        var original = await f.Completed();
        var directory = Path.GetFullPath(f.Directory);
        var journal = Path.Combine(directory, "jobs.json");
        var backup = Path.Combine(directory, "jobs-test-backup.json");
        Assert.Equal(directory, Path.GetDirectoryName(journal)); Assert.Equal(directory, Path.GetDirectoryName(backup));
        await SceneFixture.Eventually(async () => !Assert.Single(SceneJson.Read<SceneJournalDocument>(
            await File.ReadAllTextAsync(journal, TestContext.Current.CancellationToken), 1024 * 1024).Entries).PhysicalOutstanding);
        // Both paths belong to this fixture. An empty directory obstructs the
        // journal's atomic file replacement on Windows and Unix alike.
        File.Move(journal, backup);
        Directory.CreateDirectory(journal);
        try
        {
            f.Authorization = (_, grant) => Change(grant, "action");
            var failed = await f.Invoke("scene.status", Id(request));
            Assert.False(failed.Succeeded); Assert.Equal("journal_unavailable", failed.ErrorCode);
            Assert.Empty(failed.Metadata);
            f.Authorization = null;
            AssertRevoked(original, await f.Status());
            var blocked = await f.Submit(request with { RequestId = "new-blocked-request" });
            Assert.False(blocked.Succeeded); Assert.Equal("backend_work_unconfirmed", blocked.ErrorCode);
            Assert.Equal(1, f.Backend.Count);
        }
        finally { Directory.Delete(journal); File.Move(backup, journal); }
        await f.Restart();
        Assert.Empty((await f.Status()).Proposals); // Restart cannot certify old freshness even if the write was lost.
    }

    private static Dictionary<string, string> Id(SceneRequest request) => new() { ["requestId"] = request.RequestId };
    private static void AssertRevoked(SceneAnalysisResult original, SceneAnalysisResult result)
    {
        Assert.Equal(SceneJson.Write(original with { Proposals = [], FreshnessVerified = false, ErrorCode = "stale_context" }), SceneJson.Write(result));
    }
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
        "exception" => throw new InvalidOperationException("authorization_unavailable"),
        "canceled-lookup" => throw new TaskCanceledException(),
        "invalid-grant" => grant with { CallerId = "invalid caller" },
        _ => throw new InvalidOperationException(change)
    };
}
