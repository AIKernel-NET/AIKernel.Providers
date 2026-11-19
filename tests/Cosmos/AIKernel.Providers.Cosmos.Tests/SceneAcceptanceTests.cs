using System.Security.Claims;
using AIKernel.Abstractions.Capabilities;
using AIKernel.Dtos.Capabilities;

namespace AIKernel.Providers.Cosmos.Tests;

public sealed class SceneAcceptanceTests
{
    [Fact]
    public async Task A01_OfficialContractsAndIndependentCapability()
    {
        await using var f = new SceneFixture();
        Assert.IsAssignableFrom<ICapabilityModuleInvoker>(f.Session);
        Assert.Equal("AIKernel.Abstractions", typeof(ICapabilityModuleInvoker).Assembly.GetName().Name);
        Assert.Equal("AIKernel.Dtos", typeof(CapabilityInvocationRequest).Assembly.GetName().Name);
        var old = await f.Session.InvokeAsync(new("inv-old", "theoriacast.assistant.observer", "assistant.observe",
            new Dictionary<string, string>(), null, "old", new Dictionary<string, string>()), TestContext.Current.CancellationToken);
        Assert.False(old.Succeeded); Assert.Equal("capability_unsupported", old.ErrorCode); Assert.Equal(0, f.Backend.Count);
        var catalog = await f.Invoke("scene.capabilities");
        Assert.True(catalog.Succeeded);
        Assert.Contains("\"videoSupported\":false", catalog.Metadata[SceneService.ResultMetadataKey]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A02_ImageAndStateProduceStructuredAnalysis(bool empty)
    {
        await using var f = new SceneFixture();
        if (empty) f.Backend.Output = "{\"observations\":[],\"proposals\":[]}";
        var request = await f.Request();
        var accepted = await f.Submit(request);
        Assert.True(accepted.Succeeded); Assert.Equal("existing-replay-hash", accepted.ReplayLogHash);
        var result = await f.Completed();
        Assert.Equal("completed", result.Status); Assert.True(result.AnalysisOnly); Assert.True(result.Provider.IsMock);
        Assert.Equal(empty ? 0 : 1, result.Proposals.Count);
        Assert.Equal(request.CapturedAtUtc.AddMilliseconds(5000), result.ExpiresAtUtc);
        var status = await f.Invoke("scene.status", new Dictionary<string, string> { ["requestId"] = request.RequestId });
        Assert.Equal(SceneJson.Hash(status.Metadata[SceneService.ResultMetadataKey]), status.OutputHash);
    }

    [Theory]
    [InlineData("actor", "actor_revision_mismatch")]
    [InlineData("action", "action_not_authorized")]
    [InlineData("target", "target_not_authorized")]
    [InlineData("revision", "actor_revision_mismatch")]
    [InlineData("snapshot", "inconsistent_snapshot")]
    [InlineData("policy", "permission_denied")]
    [InlineData("scope", "permission_denied")]
    [InlineData("video", "media_unsupported")]
    public async Task A03_A06_InvalidRequestNeverStartsInference(string kind, string code)
    {
        await using var f = new SceneFixture();
        var r = await f.Request();
        r = kind switch
        {
            "actor" => r with { Actors = [new("someone-else", 12, "listening", true)] },
            "action" => r with { AllowedActions = [new("shell", ["actor-a"], ["board-a"])] },
            "target" => r with { Targets = [new("private-target", "board")] },
            "revision" => r with { Actors = [new("actor-a", 13, "listening", true)] },
            "snapshot" => r with { SnapshotId = "wrong-snapshot" },
            "policy" => r with { PolicyRevision = "private-policy" },
            "scope" => r with { PermissionScope = "author-only" },
            _ => r with { Media = [r.Media[0] with { MimeType = "video/mp4" }] }
        };
        var result = await f.Submit(r);
        Assert.False(result.Succeeded); Assert.Equal(code, result.ErrorCode); Assert.Equal(0, f.Backend.Count);
    }

    [Theory]
    [InlineData("{", "invalid_json")]
    [InlineData("{\"schemaVersion\":\"a\",\"schemaVersion\":\"b\"}", "duplicate_field")]
    [InlineData("{\"secret\":\"do-not-log\"}", "invalid_json")]
    public async Task A03_A10_InvalidJsonIsSanitized(string json, string code)
    {
        await using var f = new SceneFixture();
        var result = await f.Invoke("scene.analyze", new Dictionary<string, string> { ["request_json"] = json });
        Assert.False(result.Succeeded); Assert.Equal(code, result.ErrorCode); Assert.Equal(code, result.ErrorMessage);
        Assert.Empty(result.Metadata); Assert.Equal(0, f.Backend.Count);
    }

    [Theory]
    [InlineData("extra")]
    [InlineData("actor")]
    [InlineData("action")]
    [InlineData("target")]
    [InlineData("revision")]
    [InlineData("evidence")]
    [InlineData("nan")]
    [InlineData("huge")]
    public async Task A03_A07_InvalidModelOutputCannotBecomeAProposal(string change)
    {
        await using var f = new SceneFixture();
        var request = await f.Request();
        string valid = f.Backend.Reply(new(request, [])).ResultJson;
        f.Backend.Output = change switch
        {
            "extra" => valid.Insert(1, "\"script\":\"run a shell\","),
            "actor" => valid.Replace("actor-a", "other-actor"),
            "action" => valid.Replace("look_at", "mcp.invoke"),
            "target" => valid.Replace("board-a", "private-target"),
            "revision" => valid.Replace("\"assetRevision\":12", "\"assetRevision\":999"),
            "evidence" => valid.Replace("frame-1", "nonexistent-frame"),
            "nan" => valid.Replace("\"region\":null", "\"region\":{\"x\":NaN,\"y\":0,\"width\":1,\"height\":1}"),
            _ => new string('x', 65537)
        };
        Assert.True((await f.Submit(request)).Succeeded);
        var result = await f.Completed();
        Assert.Equal("failed", result.Status); Assert.Empty(result.Proposals); Assert.False(result.FreshnessVerified);
    }

    [Fact]
    public async Task A04_ConcurrentSameIdIsIdempotentAndDifferentInputConflicts()
    {
        await using var f = new SceneFixture();
        f.Backend.Block = true;
        var r = await f.Request();
        var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => f.Submit(r)));
        Assert.All(responses, response => Assert.True(response.Succeeded));
        await f.WaitForCall();
        Assert.Equal(1, f.Backend.Count);
        var conflict = await f.Submit(r with { MaxProposals = 1 });
        Assert.False(conflict.Succeeded); Assert.Equal("request_id_conflict", conflict.ErrorCode);
        f.Backend.ReleaseAll(); Assert.Equal("completed", (await f.Completed()).Status);
        await f.Restart();
        Assert.Equal("completed", (await f.Status()).Status);
        Assert.False((await f.Status()).FreshnessVerified);
        Assert.True((await f.Submit(r)).Succeeded); Assert.Equal(1, f.Backend.Count);
    }

    [Fact]
    public async Task A04_A12_CancellationKeepsPhysicalSlotAndLatestQueuedRequestWins()
    {
        await using var f = new SceneFixture();
        f.Backend.Block = true;
        await f.Submit(await f.Request("one"));
        await f.WaitForCall();
        await f.Submit(await f.Request("two"));
        await f.Submit(await f.Request("three"));
        Assert.Equal("superseded", (await f.Status("two")).Status);
        await f.Invoke("scene.cancel", new Dictionary<string, string> { ["requestId"] = "one" });
        Assert.Equal("canceled", (await f.Status("one")).Status);
        Assert.Equal("queued", (await f.Status("three")).Status); Assert.Equal(1, f.Backend.Count);
        await SceneFixture.Eventually(async () => (await f.Status("one")).BackendCancellation == "unsupported");
        Assert.Equal("unsupported", (await f.Status("one")).BackendCancellation);
        f.Backend.Block = false; f.Backend.ReleaseAll();
        Assert.Equal("completed", (await f.Completed("three")).Status);
        Assert.Equal("canceled", (await f.Status("one")).Status); Assert.Equal(2, f.Backend.Count);
    }

    [Fact]
    public async Task A04_BackendCancellationIsPropagatedWhenSupported()
    {
        await using var f = new SceneFixture(); f.Backend.Block = true; f.Backend.CancelReceipt = "confirmed";
        await f.Submit(await f.Request());
        await f.WaitForCall();
        await f.Invoke("scene.cancel", new Dictionary<string, string> { ["requestId"] = "req-1" });
        await SceneFixture.Eventually(async () => (await f.Status()).BackendCancellation == "confirmed");
        Assert.Equal("canceled", (await f.Status()).Status);
    }

    [Theory]
    [InlineData(-6000, "observation_expired")]
    [InlineData(2000, "future_capture")]
    public async Task A05_CaptureTimeControlsFreshness(int offset, string code)
    {
        await using var f = new SceneFixture();
        var result = await f.Submit(await f.Request(offset: offset));
        Assert.False(result.Succeeded); Assert.Equal(code, result.ErrorCode); Assert.Equal(0, f.Backend.Count);
    }

    [Fact]
    public async Task A05_UnknownClockAndExpiredResultsCannotSupplyUsableProposals()
    {
        await using var f = new SceneFixture(); f.ClockKnown = false;
        await f.Submit(await f.Request()); var result = await f.Completed();
        Assert.Equal("completed", result.Status); Assert.False(result.FreshnessVerified); Assert.Empty(result.Proposals);
        f.ClockKnown = true;
        await f.Submit(await f.Request("known")); Assert.Single((await f.Completed("known")).Proposals);
        f.Clock.Advance(5001);
        Assert.Empty((await f.Status("known")).Proposals); Assert.False((await f.Status("known")).FreshnessVerified);
    }

    [Fact]
    public async Task A05_MonotonicDeadlineDiscardsLateResponseAndInvalidationRevokesOldEpoch()
    {
        await using var f = new SceneFixture(); f.Backend.Block = true;
        await f.Submit(await f.Request());
        await f.WaitForCall();
        f.Clock.Advance(3001); Assert.Equal("timed_out", (await f.Status()).Status);
        f.Backend.Block = false; f.Backend.ReleaseAll();
        await f.Invoke("scene.invalidate");
        var stale = await f.Submit(await f.Request("old-epoch"));
        Assert.False(stale.Succeeded); Assert.Equal("stale_context", stale.ErrorCode);
        Assert.Empty((await f.Status()).Proposals);
    }

    [Fact]
    public async Task A06_AuthBlobScopeHashAndExpiryAreEnforced()
    {
        await using var f = new SceneFixture();
        Assert.Throws<SceneException>(() => f.Service.OpenSession(new ClaimsPrincipal(new ClaimsIdentity())));
        var r = await f.Request(); var other = f.Service.OpenSession(f.Principal("caller-b"));
        var result = await f.Submit(r, other);
        Assert.False(result.Succeeded); Assert.Equal("media_unavailable", result.ErrorCode);
        var registration = new SceneMediaRegistration(SceneJson.Snapshot(r), "new-frame", 0, "image/png", 2, 2, "sha256:bad", f.Clock.GetUtcNow().AddSeconds(1));
        var wrong = await Assert.ThrowsAsync<SceneException>(() => f.Session.RegisterMediaAsync(registration, new MemoryStream(SceneFixture.Png()), TestContext.Current.CancellationToken));
        Assert.Equal("hash_mismatch", wrong.Code);
        var tooLarge = await Assert.ThrowsAsync<SceneException>(() => f.Session.RegisterMediaAsync(registration, new MemoryStream(new byte[f.Options.MaxImageBytes + 1]), TestContext.Current.CancellationToken));
        Assert.Equal("media_too_large", tooLarge.Code);
        f.Clock.Advance(61000);
        var missing = await f.Submit(r with { CapturedAtUtc = f.Clock.GetUtcNow() });
        Assert.False(missing.Succeeded); Assert.Equal("media_unavailable", missing.ErrorCode);
    }

    [Theory]
    [InlineData("https://example.invalid/a.png")]
    [InlineData("\\\\server\\private.png")]
    [InlineData("C:\\private.png")]
    public async Task A06_ArbitraryMediaLocationsAreRejected(string mediaId)
    {
        await using var f = new SceneFixture(); var r = await f.Request();
        var result = await f.Submit(r with { Media = [r.Media[0] with { MediaId = mediaId }] });
        Assert.False(result.Succeeded); Assert.Equal("invalid_identifier", result.ErrorCode);
    }

    [Theory]
    [InlineData("loading")]
    [InlineData("warming_up")]
    [InlineData("busy")]
    [InlineData("unavailable")]
    public async Task A08_A09_ReadinessDoesNotStartInferenceOrRestartAnything(string state)
    {
        await using var f = new SceneFixture(); f.Backend.State = state;
        for (int i = 0; i < 3; i++) Assert.Equal(state, (await f.Service.ProbeAsync(TestContext.Current.CancellationToken)).State);
        await f.Submit(await f.Request()); Assert.Equal("failed", (await f.Completed()).Status);
        Assert.Equal(0, f.Backend.Count);
    }

    [Fact]
    public async Task A08_UnknownPhysicalCompletionQuarantinesFurtherInference()
    {
        await using var f = new SceneFixture(); f.Backend.Failure = new("backend_transport_unconfirmed", true);
        var request = await f.Request();
        await f.Submit(request); Assert.Equal("failed", (await f.Completed()).Status);
        Assert.Equal("degraded", (await f.Service.ProbeAsync(TestContext.Current.CancellationToken)).State);
        var response = await f.Submit(request with { RequestId = "next" });
        Assert.False(response.Succeeded); Assert.Equal("backend_work_unconfirmed", response.ErrorCode); Assert.Equal(1, f.Backend.Count);
    }

    [Fact]
    public async Task A10_NoPixelsOrSceneTextArePersistedAndAnalysisIsNotExecution()
    {
        await using var f = new SceneFixture();
        await f.Submit(await f.Request()); var result = await f.Completed();
        string stored = await File.ReadAllTextAsync(Path.Combine(f.Directory, "jobs.json"), TestContext.Current.CancellationToken);
        Assert.DoesNotContain(Convert.ToBase64String(SceneFixture.Png()), stored);
        Assert.DoesNotContain("listening", stored); Assert.DoesNotContain("presentation-surface", stored);
        Assert.True(result.AnalysisOnly); Assert.DoesNotContain("performed", SceneJson.Write(result));
    }

    [Fact]
    public async Task A11_TwoActorsRetainDistinctStableProposalIds()
    {
        await using var f = new SceneFixture(); var r = await f.Request();
        r = r with { Actors = [new("actor-a", 12, "listening", true), new("actor-b", 7, "listening", true)],
            AllowedActions = [new("look_at", ["actor-a", "actor-b"], ["board-a"])] };
        f.Backend.Output = SceneJson.Write(new SceneModelResult(
            [new("obs-a", "actor-a", "board-a", "attention_target_mismatch", false, ["frame-1"]),
             new("obs-b", "actor-b", "board-a", "attention_target_mismatch", false, ["frame-1"])],
            [new("actor-a", 12, "look_at", "board-a", ["obs-a"]), new("actor-b", 7, "look_at", "board-a", ["obs-b"])]));
        await f.Submit(r); var result = await f.Completed();
        Assert.Equal(2, result.Proposals.Count); Assert.NotEqual(result.Proposals[0].ProposalId, result.Proposals[1].ProposalId);
        Assert.Equal(SceneJson.Write(result.Proposals), SceneJson.Write((await f.Status()).Proposals));
    }

    [Fact]
    public async Task A12_TotalRetentionIsBoundedWithoutEvictingIdempotencyKeys()
    {
        await using var f = new SceneFixture(maxJobs: 1); await f.Submit(await f.Request()); await f.Completed();
        var full = await f.Submit(await f.Request("next"));
        Assert.False(full.Succeeded); Assert.Equal("job_capacity", full.ErrorCode); Assert.Equal(1, f.Backend.Count);
    }
}
