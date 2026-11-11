namespace AIKernel.Providers.Cosmos.Tests;

public sealed class SceneContextLifetimeTests
{
    [Theory]
    [InlineData("camera")]
    [InlineData("model")]
    [InlineData("asset")]
    public async Task A05_ReturningToEarlierContextCannotReviveCompletedProposal(string dimension)
    {
        await using var f = new SceneFixture();
        var firstRequest = await f.Request("req-a");
        Assert.True((await f.Submit(firstRequest)).Succeeded);
        var original = await f.Completed("req-a");
        Assert.True(original.FreshnessVerified); Assert.Single(original.Proposals);
        var originalBackend = f.Backend.Identity;

        var different = await f.Request("req-b");
        if (dimension == "camera") different = await WithCamera(f, different, "camera-close");
        if (dimension == "model") f.Backend.Identity = originalBackend with { Revision = "revision-other" };
        if (dimension == "asset")
        {
            f.ActorRevision = 13;
            different = different with { Actors = [different.Actors[0] with { AssetRevision = 13 }] };
        }
        f.Backend.Output = "{\"observations\":[],\"proposals\":[]}";
        Assert.True((await f.Submit(different)).Succeeded);
        Assert.Equal("completed", (await f.Completed("req-b")).Status);
        Assert.Empty((await f.Status("req-a")).Proposals);

        f.Backend.Identity = originalBackend; f.ActorRevision = 12; f.Backend.Output = null;
        Assert.True((await f.Submit(await f.Request("req-c"))).Succeeded);
        var current = await f.Completed("req-c");
        Assert.True(current.FreshnessVerified); Assert.Single(current.Proposals);
        var old = await f.Status("req-a");
        Assert.Equal("completed", old.Status); // Historical analysis still completed.
        Assert.Empty(old.Proposals); Assert.False(old.FreshnessVerified);
        Assert.Equal("stale_context", old.ErrorCode);
        Assert.Equal(original.ExpiresAtUtc, old.ExpiresAtUtc); // No clock advance or expiry explains the revocation.
        var retry = await f.Submit(firstRequest);
        Assert.True(retry.Succeeded); Assert.Empty(SceneFixture.Parse(retry).Proposals);
        Assert.Equal(3, f.Backend.Count);
    }

    [Fact]
    public async Task A04_UnchangedContextPreservesResultBytesAndProposalIds()
    {
        await using var f = new SceneFixture(); var first = await f.Request("req-a");
        await f.Submit(first); var before = await f.Completed("req-a");
        await f.Submit(await f.Request("req-b")); await f.Completed("req-b");
        var after = await f.Status("req-a");
        Assert.True(after.FreshnessVerified); Assert.Single(after.Proposals);
        Assert.Equal(SceneJson.Write(before), SceneJson.Write(after));
        Assert.Equal(SceneJson.Hash(SceneJson.Write(before)), SceneJson.Hash(SceneJson.Write(after)));
    }

    [Fact]
    public async Task A05_ContextChangeIsIsolatedToItsAuthenticatedCaller()
    {
        await using var f = new SceneFixture();
        var other = f.Service.OpenSession(f.Principal("caller-b"));
        await f.Submit(await f.Request("req-a")); await f.Completed("req-a");
        await f.Submit(await f.Request("req-b", other), other);
        await SceneFixture.Eventually(async () => (await f.Status("req-b", other)).Status == "completed");
        var originalOther = await f.Status("req-b", other);
        await f.Submit(await WithCamera(f, await f.Request("req-c"), "camera-close"));
        await f.Completed("req-c");
        Assert.Empty((await f.Status("req-a")).Proposals);
        Assert.Equal(SceneJson.Write(originalOther), SceneJson.Write(await f.Status("req-b", other)));
    }

    private static async Task<SceneRequest> WithCamera(SceneFixture f, SceneRequest request, string camera)
    {
        request = request with { CameraId = camera };
        var png = SceneFixture.Png();
        var media = await f.Session.RegisterMediaAsync(new(SceneJson.Snapshot(request), "frame-1", 0,
            "image/png", 2, 2, SceneJson.Hash(png), f.Clock.GetUtcNow().AddMinutes(1)), new MemoryStream(png), TestContext.Current.CancellationToken);
        return request with { Media = [new(media.MediaId, "frame-1", "image/png", 2, 2, 0)] };
    }
}
