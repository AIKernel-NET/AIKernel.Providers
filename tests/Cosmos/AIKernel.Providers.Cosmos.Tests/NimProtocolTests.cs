using System.Net;
using System.Text;
using System.Text.Json;

namespace AIKernel.Providers.Cosmos.Tests;

public sealed class NimProtocolTests
{
    [Fact]
    public void A03_ConfiguredIdentityCannotViolateResultSchema()
    {
        var options = new CosmosNimOptions
        {
            Endpoint = new("https://cosmos.invalid/"), EndpointId = "invalid endpoint label",
            Model = "model", Revision = "revision", Precision = "fp8", Runtime = "nim",
            ImageDigest = "sha256:image", GpuIds = ["GPU-test"], AllowedAddresses = [IPAddress.Loopback]
        };
        var clock = new ManualClock();
        Assert.Throws<ArgumentException>(() => new CosmosNimBackend(options, new Evidence(clock), new Handler(), clock));
    }

    private sealed class Evidence(ManualClock clock) : ISceneDeploymentEvidenceSource
    {
        internal string Gpu = "GPU-test";
        public ValueTask<SceneDeploymentEvidence> ReadAsync(CancellationToken cancellationToken) => ValueTask.FromResult(
            new SceneDeploymentEvidence("configured-model", "pinned-revision", "fp8", "nim-pinned", [Gpu], "sha256:image-pinned",
                clock.GetUtcNow(), clock.GetUtcNow().AddMinutes(1), "ready"));
    }
    private sealed class Handler : HttpMessageHandler
    {
        internal string Finish = "stop";
        internal string Reply = "{\"observations\":[],\"proposals\":[]}";
        internal string Model = "configured-model";
        internal HttpStatusCode PostStatus = HttpStatusCode.OK;
        internal string? RequestJson;
        internal int Posts;
        internal bool ThrowCancellation;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body;
            if (request.Method == HttpMethod.Post)
            {
                Posts++; RequestJson = await request.Content!.ReadAsStringAsync(cancellationToken);
                if (ThrowCancellation) throw new TaskCanceledException();
                body = JsonSerializer.Serialize(new { model = Model, choices = new[] { new { finish_reason = Finish, message = new { content = Reply } } } });
            }
            else body = request.RequestUri!.AbsolutePath switch
            {
                "/v1/health/ready" => "{}",
                "/v1/metadata" => "{\"model_type\":\"reasoner\",\"inference_endpoint\":\"/v1/chat/completions\"}",
                "/v1/models" => "{\"data\":[{\"id\":\"configured-model\"}]}",
                _ => throw new InvalidOperationException("Unexpected route")
            };
            return new(request.Method == HttpMethod.Post ? PostStatus : HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
    private static CosmosNimOptions Options(string address = "127.0.0.1") => new()
    {
        Endpoint = new("https://nim.test/"), EndpointId = "managed-nim", Model = "configured-model", Revision = "pinned-revision",
        Precision = "fp8", Runtime = "nim-pinned", ImageDigest = "sha256:image-pinned", GpuIds = ["GPU-test"], AllowedAddresses = [IPAddress.Parse(address)]
    };

    [Fact]
    public async Task A02_RealAdapterSerializesMultimodalProtocolAndReturnsBackendIdentity()
    {
        await using var fixture = new SceneFixture(); var handler = new Handler();
        using var backend = new CosmosNimBackend(Options(), new Evidence(fixture.Clock), handler, fixture.Clock);
        var readiness = await backend.ProbeAsync(TestContext.Current.CancellationToken);
        Assert.Equal("ready", readiness.State); Assert.True(readiness.DeploymentVerified); Assert.Equal(0, handler.Posts);
        var request = await fixture.Request();
        var reply = await backend.AnalyzeAsync(new(request, [SceneFixture.Png()]), TestContext.Current.CancellationToken);
        Assert.Equal("cosmos", reply.Provider.Backend); Assert.False(reply.Provider.IsMock);
        using var json = JsonDocument.Parse(handler.RequestJson!);
        var content = json.RootElement.GetProperty("messages")[1].GetProperty("content");
        Assert.Equal("image_url", content[1].GetProperty("type").GetString());
        Assert.StartsWith("data:image/png;base64,", content[1].GetProperty("image_url").GetProperty("url").GetString());
        Assert.DoesNotContain("permissionScope", handler.RequestJson!); Assert.DoesNotContain("caller-a", handler.RequestJson!);
        Assert.Equal("unsupported", await backend.RequestCancellationAsync("req-1", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("length", "response_truncated")]
    [InlineData("content_filter", "content_filtered")]
    [InlineData("tool_calls", "completion_incomplete")]
    [InlineData("unknown", "completion_incomplete")]
    public async Task A03_TruncatedFilteredOrIncompleteResponsesFail(string finish, string code)
    {
        await using var f = new SceneFixture(); var handler = new Handler { Finish = finish };
        using var backend = new CosmosNimBackend(Options(), new Evidence(f.Clock), handler, f.Clock);
        var request = await f.Request();
        var error = await Assert.ThrowsAsync<SceneException>(() => backend.AnalyzeAsync(new(request, [SceneFixture.Png()]), TestContext.Current.CancellationToken));
        Assert.Equal(code, error.Code); Assert.Equal(1, handler.Posts);
    }

    [Theory]
    [InlineData(429, "backend_busy", false)]
    [InlineData(500, "backend_unavailable", true)]
    [InlineData(408, "backend_unavailable", true)]
    [InlineData(302, "backend_request_rejected", false)]
    public async Task A08_BackpressureAndRedirectsAreNotRetried(int status, string code, bool uncertain)
    {
        await using var f = new SceneFixture(); var handler = new Handler { PostStatus = (HttpStatusCode)status };
        using var backend = new CosmosNimBackend(Options(), new Evidence(f.Clock), handler, f.Clock);
        var request = await f.Request();
        var error = await Assert.ThrowsAsync<SceneException>(() => backend.AnalyzeAsync(new(request, [SceneFixture.Png()]), TestContext.Current.CancellationToken));
        Assert.Equal(code, error.Code); Assert.Equal(uncertain, error.WorkMayStillBeRunning); Assert.Equal(1, handler.Posts);
    }

    [Fact]
    public async Task A08_TransportCancellationDoesNotClaimGpuWorkStopped()
    {
        await using var f = new SceneFixture(); var handler = new Handler { ThrowCancellation = true };
        using var backend = new CosmosNimBackend(Options(), new Evidence(f.Clock), handler, f.Clock);
        var request = await f.Request();
        var error = await Assert.ThrowsAsync<SceneException>(() => backend.AnalyzeAsync(new(request, [SceneFixture.Png()]), TestContext.Current.CancellationToken));
        Assert.Equal("backend_transport_unconfirmed", error.Code); Assert.True(error.WorkMayStillBeRunning);
    }

    [Fact]
    public async Task A09_WrongGpuFailsReadinessWithoutGeneratingOrReconfiguring()
    {
        var clock = new ManualClock(); var handler = new Handler();
        using var backend = new CosmosNimBackend(Options(), new Evidence(clock) { Gpu = "GPU-other" }, handler, clock);
        var result = await backend.ProbeAsync(TestContext.Current.CancellationToken);
        Assert.Equal("degraded", result.State); Assert.False(result.DeploymentVerified); Assert.Equal(0, handler.Posts);
    }

    [Theory]
    [InlineData("169.254.169.254")]
    [InlineData("0.0.0.0")]
    [InlineData("fe80::1")]
    [InlineData("224.0.0.1")]
    public void A06_MetadataAndNonDestinationAddressesCannotBeConfigured(string address)
    {
        var clock = new ManualClock();
        Assert.Throws<ArgumentException>(() => new CosmosNimBackend(Options(address), new Evidence(clock), new Handler(), clock));
    }
}
