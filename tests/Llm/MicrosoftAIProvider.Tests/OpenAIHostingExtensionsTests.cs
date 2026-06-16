namespace AIKernel.Providers.Tests;

using AIKernel.Abstractions.Execution;
using AIKernel.Abstractions.Models;
using AIKernel.Abstractions.Providers;
using AIKernel.Core.Security;
using AIKernel.Dtos.Core;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Routing;
using AIKernel.Dtos.Rules;
using AIKernel.Enums;
using AIKernel.Hosting;
using AIKernel.Providers.MicrosoftAI;
using AIKernel.Providers.MicrosoftAI.DependencyInjection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public sealed class OpenAIHostingExtensionsTests
{
    [Fact]
    public async Task WithOpenAI_ResolvesSecretBeforeProviderUse()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["OpenAI:ApiKey"] = "sk-config-123456",
                    ["AIKernel:Providers:OpenAI:ModelId"] = "gpt-test",
                    ["AIKernel:Providers:OpenAI:SecretKeyName"] = "OpenAI:ApiKey"
                })
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IProviderCapabilities, TestProviderCapabilities>();

        services
            .AddAIKernelCore(configuration)
            .WithOpenAI(
                configuration.GetSection("AIKernel:Providers:OpenAI"),
                (_, options) =>
                {
                    Assert.Equal("sk-config-123456", options.ApiKey);
                    return new StubChatClient("ok");
                });

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var hostedServices = provider.GetServices<IHostedService>();

        foreach (var hostedService in hostedServices)
        {
            await hostedService.StartAsync(TestContext.Current.CancellationToken);
        }

        var modelProvider = provider.GetRequiredService<IModelProvider>();

        await modelProvider.InitializeAsync();

        var output = await modelProvider.GenerateAsync(
        [
            new TestModelMessage("user", "hello")
        ],
        TestContext.Current.CancellationToken);

        Assert.Equal("ok", output);
    }

    [Fact]
    public async Task WithOpenAI_UsesDirectApiKey_WhenConfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["AIKernel:Providers:OpenAI:ModelId"] = "gpt-test",
                    ["AIKernel:Providers:OpenAI:ApiKey"] = "sk-direct-123456"
                })
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IProviderCapabilities, TestProviderCapabilities>();

        services
            .AddAIKernelCore()
            .WithOpenAI(
                configuration.GetSection("AIKernel:Providers:OpenAI"),
                (_, options) =>
                {
                    Assert.Equal("sk-direct-123456", options.ApiKey);
                    return new StubChatClient("direct-ok");
                });

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var hostedServices = provider.GetServices<IHostedService>();

        foreach (var hostedService in hostedServices)
        {
            await hostedService.StartAsync(TestContext.Current.CancellationToken);
        }

        var modelProvider = provider.GetRequiredService<IModelProvider>();

        await modelProvider.InitializeAsync();

        var output = await modelProvider.GenerateAsync(
        [
            new TestModelMessage("user", "hello")
        ],
        TestContext.Current.CancellationToken);

        Assert.Equal("direct-ok", output);
    }

    [Fact]
    public void WithOpenAI_RegistersPromptCapabilityForKernelExecution()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["AIKernel:Providers:OpenAI:ProviderId"] = "openai-demo",
                    ["AIKernel:Providers:OpenAI:ModelId"] = "gpt-demo",
                    ["AIKernel:Providers:OpenAI:ApiKey"] = "sk-direct-123456",
                    ["AIKernel:Providers:OpenAI:MaxInputTokens"] = "4096",
                    ["AIKernel:Providers:OpenAI:MaxOutputTokens"] = "512"
                })
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IProviderCapabilities, TestProviderCapabilities>();

        services
            .AddAIKernelCore()
            .WithOpenAI(
                configuration.GetSection("AIKernel:Providers:OpenAI"),
                (_, _) => new StubChatClient("ok"));

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var capability = provider.GetRequiredService<ModelPromptCapability>();

        Assert.Equal("openai-demo", capability.ProviderId);
        Assert.Equal("gpt-demo", capability.ModelId);
        Assert.Equal(4096, capability.MaxInputTokens);
        Assert.Equal(512, capability.MaxOutputTokens);
        Assert.Contains("user", capability.SupportedRoles);
        Assert.Contains("system", capability.SupportedRoles);
    }

    [Fact]
    public void WithOpenAI_RegisteredPromptCapabilityIsResolvable()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["AIKernel:Providers:OpenAI:ProviderId"] = "openai-demo",
                    ["AIKernel:Providers:OpenAI:ModelId"] = "gpt-demo",
                    ["AIKernel:Providers:OpenAI:ApiKey"] = "sk-direct-123456"
                })
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IProviderCapabilities, TestProviderCapabilities>();

        services
            .AddAIKernelCore()
            .WithOpenAI(
                configuration.GetSection("AIKernel:Providers:OpenAI"),
                (_, _) => new StubChatClient("ok"));

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var modelProvider = provider.GetRequiredService<IModelProvider>();
        var resolver = provider.GetRequiredService<IModelPromptCapabilityResolver>();

        var capability = resolver.Resolve(
            modelProvider,
            new KernelExecutionRequest
            {
                ContextSnapshotId = "snapshot:openai-hosting",
                ContextHash = "sha256:openai-hosting",
                ContextBlocks = [],
                UserInstruction = "hello",
                PromptOptions = CreatePromptOptions(),
                ExecutionOptions = CreateExecutionOptions(),
                RequestedModelId = "gpt-demo"
            });

        Assert.Equal("openai-demo", capability.ProviderId);
        Assert.Equal("gpt-demo", capability.ModelId);
    }

    [Fact]
    public void WithOpenAI_RegistersProviderCapabilityInterfaces()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["AIKernel:Providers:OpenAI:ModelId"] = "gpt-demo",
                    ["AIKernel:Providers:OpenAI:ApiKey"] = "sk-direct-123456"
                })
            .Build();

        var services = new ServiceCollection();

        services
            .AddAIKernelCore()
            .WithOpenAI(
                configuration.GetSection("AIKernel:Providers:OpenAI"),
                (_, _) => new StubChatClient("ok"));

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var modelProvider = provider.GetRequiredService<IModelProvider>();

        Assert.Same(
            modelProvider,
            provider.GetRequiredService<IProvider>());
        Assert.Same(
            modelProvider,
            provider.GetRequiredService<IProviderIdentity>());
        Assert.Same(
            modelProvider,
            provider.GetRequiredService<IProviderCapabilitySource>());
        Assert.Same(
            modelProvider,
            provider.GetRequiredService<IProviderAvailabilityProbe>());
        Assert.Same(
            modelProvider,
            provider.GetRequiredService<IProviderLifecycle>());
        Assert.Same(
            modelProvider,
            provider.GetRequiredService<IProviderHealthProbe>());
        Assert.Same(
            modelProvider,
            provider.GetRequiredService<ITextGenerationProvider>());
        Assert.Same(
            modelProvider,
            provider.GetRequiredService<IStreamingGenerationProvider>());
        Assert.Same(
            modelProvider,
            provider.GetRequiredService<IQuestionAnsweringProvider>());
    }

    [Fact]
    public async Task WithOpenAI_HydratesProviderAndCapabilityRegistries()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["AIKernel:Providers:OpenAI:ProviderId"] = "openai-demo",
                    ["AIKernel:Providers:OpenAI:ModelId"] = "gpt-demo",
                    ["AIKernel:Providers:OpenAI:ApiKey"] = "sk-direct-123456",
                    ["AIKernel:Providers:OpenAI:MaxInputTokens"] = "4096",
                    ["AIKernel:Providers:OpenAI:MaxOutputTokens"] = "512"
                })
            .Build();

        var services = new ServiceCollection();

        services
            .AddAIKernelCore()
            .WithOpenAI(
                configuration.GetSection("AIKernel:Providers:OpenAI"),
                (_, _) => new StubChatClient("ok"));

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var providerRegistry = provider.GetRequiredService<IProviderRegistry>();
        var capabilityRegistry = provider.GetRequiredService<AIKernel.Abstractions.Routing.ICapabilityRegistry>();
        var capacity = await capabilityRegistry.GetCapabilityAsync(
            "openai-demo",
            TestContext.Current.CancellationToken);
        var candidates = await capabilityRegistry.ResolveCandidatesAsync(
            new RuleEvaluationContext(
                "context",
                "routing",
                new Dictionary<string, string>()),
            TestContext.Current.CancellationToken);

        Assert.Contains("openai-demo", providerRegistry.GetRegisteredProviders());
        Assert.NotNull(capacity);
        Assert.Equal(["openai-demo"], candidates);
    }

    [Fact]
    public async Task WithOpenAI_RegistersDefaultProviderCapabilities()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["AIKernel:Providers:OpenAI:ModelId"] = "gpt-demo",
                    ["AIKernel:Providers:OpenAI:ApiKey"] = "sk-direct-123456"
                })
            .Build();

        var services = new ServiceCollection();

        services
            .AddAIKernelCore()
            .WithOpenAI(
                configuration.GetSection("AIKernel:Providers:OpenAI"),
                (_, _) => new StubChatClient("ok"));

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var capabilities = provider.GetRequiredService<IProviderCapabilities>();
        var modelProvider = provider.GetRequiredService<IModelProvider>();

        await modelProvider.InitializeAsync();

        Assert.IsType<OpenAICompatibleProviderCapabilities>(capabilities);
        Assert.True(capabilities.SupportsOperation("chat"));
        Assert.True(capabilities.SupportsDataType("text"));
        Assert.True(await modelProvider.IsAvailableAsync());
    }

    [Fact]
    public void WithOpenAI_DoesNotReplaceCustomProviderCapabilities()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["AIKernel:Providers:OpenAI:ModelId"] = "gpt-demo",
                    ["AIKernel:Providers:OpenAI:ApiKey"] = "sk-direct-123456"
                })
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IProviderCapabilities, TestProviderCapabilities>();

        services
            .AddAIKernelCore()
            .WithOpenAI(
                configuration.GetSection("AIKernel:Providers:OpenAI"),
                (_, _) => new StubChatClient("ok"));

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var capabilities = provider.GetRequiredService<IProviderCapabilities>();

        Assert.IsType<TestProviderCapabilities>(capabilities);
    }

    [Fact]
    public void OpenAIOptionsValidator_FailsClosed_ForInvalidTokenLimits()
    {
        var validator = new OpenAICompatibleProviderOptionsValidator();

        var result = validator.Validate(
            name: null,
            new OpenAICompatibleProviderOptions
            {
                ModelId = "gpt-demo",
                ApiKey = "sk-direct-123456",
                MaxInputTokens = 0,
                MaxOutputTokens = 0
            });

        Assert.True(result.Failed);
        Assert.Contains(
            "MaxInputTokens must be greater than zero.",
            result.Failures);
        Assert.Contains(
            "MaxOutputTokens must be greater than zero when specified.",
            result.Failures);
    }

    [Fact]
    public async Task WithOpenAI_FailsClosed_WhenSecretIsMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["AIKernel:Providers:OpenAI:ModelId"] = "gpt-test",
                    ["AIKernel:Providers:OpenAI:SecretKeyName"] = "OpenAI:ApiKey"
                })
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IProviderCapabilities, TestProviderCapabilities>();

        services
            .AddAIKernelCore(configuration)
            .WithOpenAI(
                configuration.GetSection("AIKernel:Providers:OpenAI"),
                (_, _) => new StubChatClient("ok"));

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var hostedServices = provider.GetServices<IHostedService>();

        await Assert.ThrowsAsync<SecureCredentialNotFoundException>(
            async () =>
            {
                foreach (var hostedService in hostedServices)
                {
                    await hostedService.StartAsync(TestContext.Current.CancellationToken);
                }
            });
    }

    [Fact]
    public void WithOpenAI_Extension_BelongsToProviderAssembly()
    {
        var assembly = typeof(
            AIKernel.Providers.MicrosoftAI.DependencyInjection.OpenAIHostingExtensions
        ).Assembly;

        Assert.Equal(
            "AIKernel.Providers.MicrosoftAI",
            assembly.GetName().Name);
    }

    [Fact]
    public void MicrosoftAIProvider_DoesNotReferenceKernelFacadeOrHosting()
    {
        var assembly = typeof(
            AIKernel.Providers.MicrosoftAI.DependencyInjection.OpenAIHostingExtensions
        ).Assembly;

        var referencedAssemblies = assembly
            .GetReferencedAssemblies()
            .Select(x => x.Name)
            .ToArray();

        Assert.DoesNotContain(
            "AIKernel.Kernel",
            referencedAssemblies);

        Assert.DoesNotContain(
            "AIKernel.Hosting",
            referencedAssemblies);
    }

    private sealed record TestModelMessage(
        string Role,
        string Content) : IModelMessage;

    private static PromptGenerationOptions CreatePromptOptions()
    {
        return new PromptGenerationOptions
        {
            OverflowPolicy = PromptOverflowPolicy.FailClosed,
            IncludeContextHash = true,
            IncludeSourceMetadata = true
        };
    }

    private static ExecutionOptions CreateExecutionOptions()
    {
        return new ExecutionOptions
        {
            Temperature = 0,
            TopP = 1,
            MaxOutputTokens = 128,
            StopSequences = []
        };
    }

    private sealed class StubChatClient : IChatClient
    {
        private readonly string _output;

        public StubChatClient(string output)
        {
            _output = output;
        }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new ChatResponse(
                    new ChatMessage(ChatRole.Assistant, _output))
                {
                    ModelId = options?.ModelId
                });
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            return null;
        }

        public void Dispose()
        {
        }
    }

    private sealed class TestProviderCapabilities : IProviderCapabilities
    {
        public IReadOnlyList<string> SupportedOperations => [];

        public IReadOnlyList<string> SupportedDataTypes => [];

        public int MaxConcurrentConnections => 1;

        public RateLimitInfo? RateLimit => null;

        public ModelCapacityVector Vector => new();

        public IDictionary<string, float>? GetDynamicCapacities(IExecutionConstraints constraints)
        {
            return null;
        }

        public ICapabilityProfile? GetCapabilityProfile()
        {
            return null;
        }

        public bool SupportsOperation(string operation)
        {
            return false;
        }

        public bool SupportsDataType(string dataType)
        {
            return false;
        }

        public bool SupportsQuantization(string quantizationLevel)
        {
            return false;
        }

        public bool SupportsQueryAugmentation => false;

        public bool SupportsQueryDecomposition => false;

        public bool SupportsQueryRouting => false;

        public int MaxQueryParts => 0;

        public IReadOnlyList<string> SupportedQueryProcessingOperations => [];

        public bool SupportsQueryProcessingOperation(string operation)
        {
            return false;
        }

        public bool SupportsEmbedding => false;

        public int? EmbeddingDimensions => null;

        public IReadOnlyList<string> SupportedEmbeddingModels => [];
    }
}
