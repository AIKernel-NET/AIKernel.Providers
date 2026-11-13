using System.Net;
using System.Text;
using System.Text.Json;
using AIKernel.Abstractions.Providers;
using AIKernel.Providers.ChatOpenAI;

namespace AIKernel.Providers.Tests;

public sealed class ChatCompletionBudgetTests
{
    [Fact]
    public async Task SendsTokenCapAndReturnsUsageAndTruncation()
    {
        using var http = new HttpClient(new Handler(async (request, token) =>
        {
            Assert.Equal("https://test.example/v1/chat/completions", request.RequestUri!.AbsoluteUri);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            Assert.Equal(256, body.RootElement.GetProperty("max_completion_tokens").GetInt32());
            return Reply("""{"choices":[{"message":{"role":"assistant","content":"partial"},"finish_reason":"length"}],"usage":{"prompt_tokens":80,"completion_tokens":256}}""");
        }));
        var result = await new ChatOpenAIClient(http, Settings()).CompleteDetailedAsync([new Message("user", "test")], TestContext.Current.CancellationToken);
        Assert.Equal("partial", result.Content);
        Assert.Equal("length", result.FinishReason);
        Assert.Equal(80, result.PromptTokens);
        Assert.Equal(256, result.CompletionTokens);
    }

    [Fact]
    public async Task MissingUsageRemainsUnknown()
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Reply("""{"choices":[{"message":{"content":"ok"}}]}"""))));
        var result = await new ChatOpenAIClient(http, Settings()).CompleteDetailedAsync([new Message("user", "test")], TestContext.Current.CancellationToken);
        Assert.Null(result.PromptTokens);
        Assert.Null(result.CompletionTokens);
    }

    [Fact]
    public async Task InvalidCapDoesNotSendRequest()
    {
        using var http = new HttpClient(new Handler((_, _) => throw new InvalidOperationException("must not send")));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new ChatOpenAIClient(http, Settings() with { MaxCompletionTokens = 0 })
            .CompleteAsync([new Message("user", "test")], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CompletionHonorsConfiguredTimeout()
    {
        using var http = new HttpClient(new Handler(async (_, token) => { await Task.Delay(Timeout.Infinite, token); return Reply("{}"); }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ChatOpenAIClient(http, Settings() with { Timeout = TimeSpan.FromMilliseconds(30) })
            .CompleteAsync([new Message("user", "test")], TestContext.Current.CancellationToken));
    }

    private static ChatOpenAISettings Settings() => new() { Endpoint = new Uri("https://test.example/v1"), MaxCompletionTokens = 256 };
    private static HttpResponseMessage Reply(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    private sealed record Message(string Role, string Content) : IModelMessage;
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
