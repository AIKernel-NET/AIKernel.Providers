using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIKernel.Dtos.Capabilities;
using AIKernel.Providers.ChatOpenAI;

namespace AIKernel.Providers.Tests;

public sealed class ChatOpenAIEmbeddingTests
{
    [Fact]
    public async Task CapabilityInvokesConfiguredProviderAndReturnsTheVectorAndHash()
    {
        using var handler = new EmbeddingHandler();
        using var http = new HttpClient(handler);
        var settings = new ChatOpenAISettings
        {
            Endpoint = new Uri("https://test.example/v1"),
            EmbeddingModel = "configured-embedding",
            ApiKey = "test-only"
        };
        var provider = new global::AIKernel.Providers.ChatOpenAI.ChatOpenAIProvider(
            settings, new ChatOpenAIClient(http, settings));
        await provider.InitializeAsync();
        var result = await new ChatOpenAIInvoker(provider).InvokeAsync(Request(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(1, handler.Calls);
        Assert.Equal("[0.25,-0.5,1.75]", result.Metadata["embedding"]);
        Assert.Equal("3", result.Metadata["dimensions"]);
        Assert.Equal("sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("[0.25,-0.5,1.75]"))),
            result.OutputHash);
        Assert.Equal("sha256:replay", result.ReplayLogHash);
    }

    [Fact]
    public async Task UnboundCapabilityRejectsEmbeddingAndDoesNotEchoAnUntrustedVector()
    {
        var result = await new ChatOpenAIInvoker().InvokeAsync(Request(), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("OPENAI_EMBEDDING_NOT_CONFIGURED", result.ErrorCode);
        Assert.Null(result.OutputHash);
        Assert.DoesNotContain("embedding", result.Metadata.Keys);
        Assert.DoesNotContain("dimensions", result.Metadata.Keys);
    }

    private static CapabilityInvocationRequest Request() => new(
        "embedding-invocation", "providers.openai", "embedding",
        new Dictionary<string, string> { ["input"] = "embed this", ["model"] = "ignored-override" },
        null, "sha256:replay", new Dictionary<string, string> { ["embedding"] = "[99]", ["dimensions"] = "1" });

    private sealed class EmbeddingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://test.example/v1/embeddings", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("test-only", request.Headers.Authorization.Parameter);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Assert.Equal("configured-embedding", body.RootElement.GetProperty("model").GetString());
            Assert.Equal("float", body.RootElement.GetProperty("encoding_format").GetString());
            Assert.Equal("embed this", body.RootElement.GetProperty("input")[0].GetString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":[{"index":0,"embedding":[0.25,-0.5,1.75]}]}""",
                    Encoding.UTF8, "application/json")
            };
        }
    }
}
