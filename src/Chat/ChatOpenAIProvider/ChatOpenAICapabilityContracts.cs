using AIKernel.Dtos.Capabilities;
using AIKernel.Enums;

namespace AIKernel.Providers.ChatOpenAI;

/// <summary>
/// [EN] Creates AIKernel capability contracts for the ChatOpenAI provider.
/// [JA] ChatOpenAI Provider 用の AIKernel capability contract を作成します。
/// </summary>
public static class ChatOpenAICapabilityContracts
{
    /// <summary>
    /// [EN] Converts a ChatOpenAI descriptor into a public CapabilityModuleDescriptor.
    /// [JA] ChatOpenAI descriptor を公開 CapabilityModuleDescriptor に変換します。
    /// </summary>
    public static CapabilityModuleDescriptor ToContract(
        ChatOpenAICapabilityDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return new CapabilityModuleDescriptor(
            descriptor.CapabilityId,
            "Chat OpenAI Provider",
            CapabilityModuleKind.RemoteEndpoint,
            CapabilityInvocationMode.Remote,
            GetRequiredMetadataValue(descriptor.Metadata, "version", "0.1.1"),
            GetMetadataValue(descriptor.Metadata, "endpoint", null),
            null,
            null,
            [
                "chat.completion",
                "embedding",
                "moderation"
            ],
            ["network.egress", "llm.remote"],
            descriptor.Metadata);
    }

    private static string? GetMetadataValue(
        IReadOnlyDictionary<string, string> metadata,
        string key,
        string? fallback)
        => metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;

    private static string GetRequiredMetadataValue(
        IReadOnlyDictionary<string, string> metadata,
        string key,
        string fallback)
        => metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
}
