namespace AIKernel.Providers.Tests;

using AIKernel.Abstractions.Providers;
using AIKernel.Providers.MicrosoftAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class ProviderCancellationTests
{
    [Theory]
    [InlineData("operation")]
    [InlineData("task")]
    [InlineData("derived")]
    [InlineData("linked")]
    public async Task GeneratePreservesCancellationTypeAndOriginalToken(string kind)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); linked.Cancel();
        var token = kind == "linked" ? linked.Token : CancellationToken.None;
        OperationCanceledException expected = kind switch { "task" or "linked" => new TaskCanceledException("test", null, token), "derived" => new DerivedCancellation(token), _ => new("test", token) };
        var provider = Create(expected); await provider.InitializeAsync();
        var pending = provider.GenerateAsync([new Message("user", "test")], TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Same(expected, error); Assert.Equal(token, error.CancellationToken); Assert.True(pending.IsCanceled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrdinaryFailureAndTimeoutRemainProviderFailures(bool timeout)
    {
        Exception expected = timeout ? new TimeoutException("test") : new InvalidOperationException("test");
        var provider = Create(expected); await provider.InitializeAsync();
        var error = await Assert.ThrowsAnyAsync<ProviderExecutionException>(() => provider.GenerateAsync([new Message("user", "test")], TestContext.Current.CancellationToken));
        if (timeout) Assert.IsType<ProviderExecutionTimeoutException>(error); else Assert.IsType<ProviderApiException>(error);
        Assert.Same(expected, error.InnerException);
    }
    private static OpenAICompatibleProvider Create(Exception error) => new(new FailingClient(error), new OpenAICompatibleProviderCapabilities(),
        new OpenAICompatibleResponseMapper(), new OpenAICompatibleProviderOptions { ModelId = "test", ApiKey = "test-only" }, NullLogger<OpenAICompatibleProvider>.Instance);
    private sealed record Message(string Role, string Content) : IModelMessage;
    private sealed class DerivedCancellation(CancellationToken token) : OperationCanceledException("test", token);
    private sealed class FailingClient(Exception error) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => Task.FromException<ChatResponse>(error);
        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        { await Task.CompletedTask; yield break; }
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
