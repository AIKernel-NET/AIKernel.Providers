namespace AIKernel.Providers.Tests;

using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Core;
using AIKernel.Providers.MicrosoftAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class MicrosoftAIProviderContractTests
{
    [Fact]
    public async Task Provider_FailsClosed_WhenInvokedBeforeInitialization()
    {
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<ProviderApiException>(
            () => provider.GenerateAsync(
            [
                new TestModelMessage("user", "hello")
            ],
            TestContext.Current.CancellationToken));

        Assert.Contains(
            "Provider has not been initialized.",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProviderHealth_UsesCurrentInitializationState()
    {
        OpenAICompatibleProviderHealthContext? observedContext = null;

        var provider = CreateProvider(
            context =>
            {
                observedContext = context;
                return new ProviderHealthStatus(
                    IsHealthy: context.IsInitialized,
                    Message: context.IsInitialized ? "OK" : "Not initialized",
                    CheckedAt: context.CheckedAtUtc.UtcDateTime,
                    ResponseTimeMs: 0);
            });

        await provider.InitializeAsync();

        var health = await provider.GetHealthAsync();

        Assert.True(health.IsHealthy);
        Assert.NotNull(observedContext);
        Assert.True(observedContext.IsInitialized);
        Assert.Equal("openai-compatible", observedContext.ProviderId);
        Assert.Equal("gpt-test", observedContext.ModelId);
    }

    [Fact]
    public async Task ProviderHealth_FailsClosed_WhenHealthFactoryIsMissing()
    {
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<ProviderApiException>(
            () => provider.GetHealthAsync());

        Assert.Contains(
            "Provider health status factory is not configured.",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ProviderAssembly_DoesNotReferenceTools()
    {
        var referenced = typeof(OpenAICompatibleProvider).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();

        Assert.DoesNotContain("AIKernel.Tools", referenced);
        Assert.DoesNotContain(
            referenced,
            name => name is not null && name.StartsWith("AIKernel.Tools.", StringComparison.Ordinal));
    }

    [Fact]
    public void Options_ToString_RedactsApiKey()
    {
        var options = new OpenAICompatibleProviderOptions
        {
            ApiKey = "sk-test-secret",
            ModelId = "gpt-test"
        };

        var text = options.ToString();

        Assert.DoesNotContain("sk-test-secret", text, StringComparison.Ordinal);
        Assert.Contains("***REDACTED***", text, StringComparison.Ordinal);
    }

    private static OpenAICompatibleProvider CreateProvider()
        => CreateProvider(healthStatusFactory: null);

    private static OpenAICompatibleProvider CreateProvider(
        Func<OpenAICompatibleProviderHealthContext, ProviderHealthStatus>? healthStatusFactory)
        => new(
            new StubChatClient(),
            new OpenAICompatibleProviderCapabilities(),
            new OpenAICompatibleResponseMapper(),
            new OpenAICompatibleProviderOptions
            {
                ModelId = "gpt-test",
                ApiKey = "sk-test-123456",
                HealthStatusFactory = healthStatusFactory
            },
            NullLogger<OpenAICompatibleProvider>.Instance);

    private sealed record TestModelMessage(
        string Role,
        string Content) : IModelMessage;

    private sealed class StubChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(
                new ChatResponse(
                    new ChatMessage(ChatRole.Assistant, "ok"))
                {
                    ModelId = options?.ModelId
                });

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
            => null;

        public void Dispose()
        {
        }
    }
}
