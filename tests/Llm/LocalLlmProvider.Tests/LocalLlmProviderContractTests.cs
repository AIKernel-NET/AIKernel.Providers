using AIKernel.Dtos.Capabilities;
using AIKernel.Enums;
using AIKernel.Providers.LocalLlm;

namespace AIKernel.Providers.Tests;

public sealed class LocalLlmProviderContractTests
{
    [Fact]
    public void ToContract_ExposesLocalLlmOperations()
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["version"] = "0.1.1",
            ["runtime"] = "ollama",
            ["runtime_uri"] = "ollama://localhost"
        };

        var contract = LocalLlmCapabilityContracts.ToContract(
            new LocalLlmCapabilityDescriptor(
                "local-llm",
                "ollama",
                metadata));

        Assert.Equal("local-llm", contract.CapabilityId);
        Assert.Equal("Local LLM Provider", contract.Name);
        Assert.Equal(CapabilityModuleKind.ManagedAssembly, contract.Kind);
        Assert.Equal(CapabilityInvocationMode.AssemblyReference, contract.InvocationMode);
        Assert.Equal("ollama", contract.EntryPoint);
        Assert.Equal("ollama://localhost", contract.ArtifactUri);
        Assert.Equal(["chat.local", "embedding.local"], contract.ProvidedOperations);
        Assert.Equal(["local.process", "llm.local"], contract.RequiredPermissions);
    }

    [Fact]
    public async Task Provider_LifecycleAndCapabilitiesRemainContractPure()
    {
        var provider = new global::AIKernel.Providers.LocalLlm.LocalLlmProvider(new LocalLlmSettings
        {
            ProviderId = "local-llm",
            Runtime = "ollama"
        });

        Assert.False(await provider.IsAvailableAsync());
        await provider.InitializeAsync();

        Assert.True(await provider.IsAvailableAsync());
        Assert.True(provider.GetCapabilities().SupportsOperation("chat.local"));
        Assert.True(provider.GetCapabilities().SupportsOperation("embedding.local"));
        Assert.Equal("local-llm", provider.ToCapabilityDescriptor().CapabilityId);

        await provider.ShutdownAsync();
        Assert.False(await provider.IsAvailableAsync());
    }

    [Fact]
    public void ProviderAssembly_DoesNotReferenceTools()
    {
        var referenced = typeof(global::AIKernel.Providers.LocalLlm.LocalLlmProvider).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();

        Assert.DoesNotContain("AIKernel.Tools", referenced);
        Assert.DoesNotContain(referenced, name => name is not null && name.StartsWith("AIKernel.Tools.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Invoker_RejectsUnsupportedOperationFailClosed()
    {
        var invoker = new LocalLlmInvoker();

        var result = await invoker.InvokeAsync(new CapabilityInvocationRequest(
            "invoke-1",
            "local-llm",
            "unknown.operation",
            new Dictionary<string, string>(),
            null,
            "sha256:replay",
            new Dictionary<string, string>()),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("LOCAL_LLM_OPERATION_NOT_SUPPORTED", result.ErrorCode);
    }

    [Fact]
    public void ProviderManifest_IncludesCliInstallSettings()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "local-llm.provider.json");

        Assert.True(File.Exists(path));

        var json = File.ReadAllText(path);
        Assert.Contains("\"cli\"", json);
        Assert.Contains("\"command\": \"local-llm\"", json);
        Assert.Contains("\"defaultOperation\": \"chat.local\"", json);
    }
}
