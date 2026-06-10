using AIKernel.Enums;
using AIKernel.Providers.ChatOpenAI;
using AIKernel.Dtos.Capabilities;

namespace AIKernel.Providers.Tests;

public sealed class ChatOpenAIProviderContractTests
{
    [Fact]
    public void ToContract_ExposesOpenAIProviderOperations()
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["version"] = "0.1.1",
            ["endpoint"] = "https://api.openai.com/v1",
            ["model"] = "gpt-test"
        };

        var contract = ChatOpenAICapabilityContracts.ToContract(
            new ChatOpenAICapabilityDescriptor(
                "providers.openai",
                "gpt-test",
                metadata));

        Assert.Equal("providers.openai", contract.CapabilityId);
        Assert.Equal("Chat OpenAI Provider", contract.Name);
        Assert.Equal(CapabilityModuleKind.RemoteEndpoint, contract.Kind);
        Assert.Equal(CapabilityInvocationMode.Remote, contract.InvocationMode);
        Assert.Equal("https://api.openai.com/v1", contract.EntryPoint);
        Assert.Equal(
            ["chat.completion", "embedding", "moderation"],
            contract.ProvidedOperations);
        Assert.Equal(["network.egress", "llm.remote"], contract.RequiredPermissions);
    }

    [Fact]
    public async Task Provider_LifecycleAndCapabilitiesRemainContractPure()
    {
        var provider = new global::AIKernel.Providers.ChatOpenAI.ChatOpenAIProvider(new ChatOpenAISettings
        {
            ProviderId = "providers.openai",
            Model = "gpt-test"
        });

        Assert.False(await provider.IsAvailableAsync());
        await provider.InitializeAsync();

        Assert.True(await provider.IsAvailableAsync());
        Assert.True(provider.GetCapabilities().SupportsOperation("chat.completion"));
        Assert.True(provider.GetCapabilities().SupportsOperation("embedding"));
        Assert.True(provider.GetCapabilities().SupportsOperation("moderation"));
        Assert.Equal("providers.openai", provider.ToCapabilityDescriptor().CapabilityId);

        await provider.ShutdownAsync();
        Assert.False(await provider.IsAvailableAsync());
    }

    [Fact]
    public void ProviderAssembly_DoesNotReferenceTools()
    {
        var referenced = typeof(global::AIKernel.Providers.ChatOpenAI.ChatOpenAIProvider).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();

        Assert.DoesNotContain("AIKernel.Tools", referenced);
        Assert.DoesNotContain(referenced, name => name is not null && name.StartsWith("AIKernel.Tools.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Invoker_RejectsUnsupportedOperationFailClosed()
    {
        var invoker = new ChatOpenAIInvoker();

        var result = await invoker.InvokeAsync(new CapabilityInvocationRequest(
            "invoke-1",
            "openai.chat",
            "unknown.operation",
            new Dictionary<string, string>(),
            null,
            "sha256:replay",
            new Dictionary<string, string>()),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("OPENAI_OPERATION_NOT_SUPPORTED", result.ErrorCode);
    }

    [Fact]
    public void ProviderManifest_IncludesCliSettings()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "openai.provider.json");

        Assert.True(File.Exists(path));

        var json = File.ReadAllText(path);
        Assert.Contains("\"cli\"", json);
        Assert.Contains("\"defaultOperation\": \"chat.completion\"", json);
        Assert.Contains("\"OPENAI_API_KEY\"", json);
    }

    [Fact]
    public void Settings_MetadataIsDeterministicallyOrdered()
    {
        var settings = new ChatOpenAISettings
        {
            Endpoint = new Uri("https://api.openai.com/v1/"),
            Model = "gpt-test",
            EmbeddingModel = "text-embedding-test"
        };

        Assert.Equal(
            ["embedding_model", "endpoint", "model", "version"],
            settings.ToMetadata().Keys.ToArray());
    }
}
