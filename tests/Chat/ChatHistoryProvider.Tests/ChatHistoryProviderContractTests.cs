using AIKernel.Dtos.Capabilities;
using AIKernel.Enums;
using AIKernel.Providers.ChatHistory;

namespace AIKernel.Providers.Tests;

public sealed class ChatHistoryProviderContractTests
{
    [Fact]
    public void ToContract_ExposesChatHistoryOperations()
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["version"] = "0.1.1",
            ["source_uri"] = "rom://providers/chat-history/history.json"
        };

        var contract = ChatHistoryCapabilityContracts.ToContract(
            new ChatHistoryCapabilityDescriptor("chat-history", metadata));

        Assert.Equal("chat-history", contract.CapabilityId);
        Assert.Equal("Chat History Provider", contract.Name);
        Assert.Equal(CapabilityModuleKind.ManagedAssembly, contract.Kind);
        Assert.Equal(CapabilityInvocationMode.AssemblyReference, contract.InvocationMode);
        Assert.Equal("AIKernel.Providers.ChatHistory", contract.EntryPoint);
        Assert.Equal(
            ["chat.history.read", "chat.history.filter", "chat.history.latest"],
            contract.ProvidedOperations);
        Assert.Equal(["history.read", "chat.read"], contract.RequiredPermissions);
    }

    [Fact]
    public async Task Provider_LifecycleAndRecordsRemainContractPure()
    {
        var provider = new global::AIKernel.Providers.ChatHistory.ChatHistoryProvider(
            new ChatHistorySettings { ProviderId = "chat-history" },
            [
                new ChatHistoryRecord
                {
                    Role = "user",
                    Content = "hello",
                    Timestamp = DateTimeOffset.Parse("2026-06-09T00:00:00Z")
                }
            ]);

        Assert.False(await provider.IsAvailableAsync());
        await provider.InitializeAsync();

        Assert.True(await provider.IsAvailableAsync());
        Assert.True(provider.GetCapabilities().SupportsOperation("chat.history.read"));
        Assert.Single(provider.GetAll());
        Assert.Single(provider.GetByRole("USER"));
        Assert.NotNull(global::AIKernel.Providers.ChatHistory.ChatHistoryProvider.GetLatest(provider.GetRecords()));

        await provider.ShutdownAsync();
        Assert.False(await provider.IsAvailableAsync());
    }

    [Fact]
    public void ProviderAssembly_DoesNotReferenceTools()
    {
        var referenced = typeof(global::AIKernel.Providers.ChatHistory.ChatHistoryProvider).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();

        Assert.DoesNotContain("AIKernel.Tools", referenced);
        Assert.DoesNotContain(referenced, name => name is not null && name.StartsWith("AIKernel.Tools.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Invoker_RejectsUnsupportedOperationFailClosed()
    {
        var invoker = new ChatHistoryInvoker();

        var result = await invoker.InvokeAsync(new CapabilityInvocationRequest(
            "invoke-1",
            "chat-history",
            "unknown.operation",
            new Dictionary<string, string>(),
            null,
            "sha256:replay",
            new Dictionary<string, string>()),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("CHAT_HISTORY_OPERATION_NOT_SUPPORTED", result.ErrorCode);
    }
}
