using AIKernel.Dtos.Capabilities;
using AIKernel.Enums;

namespace AIKernel.Providers.ChatHistory;

/// <summary>
/// [EN] Contract mapper for chat history provider capabilities.
/// [JA] ChatHistory Provider capability の contract mapper です。
/// </summary>
public static class ChatHistoryCapabilityContracts
{
    /// <summary>
    /// [EN] Converts a chat history descriptor into the shared capability module contract.
    /// [JA] ChatHistory descriptor を共有 capability module contract へ変換します。
    /// </summary>
    public static CapabilityModuleDescriptor ToContract(
        ChatHistoryCapabilityDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return new CapabilityModuleDescriptor(
            descriptor.CapabilityId,
            "Chat History Provider",
            CapabilityModuleKind.ManagedAssembly,
            CapabilityInvocationMode.AssemblyReference,
            GetRequiredMetadataValue(descriptor.Metadata, "version", "0.1.3"),
            "AIKernel.Providers.ChatHistory",
            GetMetadataValue(descriptor.Metadata, "source_uri", null),
            GetMetadataValue(descriptor.Metadata, "artifact_hash", null),
            [
                "chat.history.read",
                "chat.history.filter",
                "chat.history.latest"
            ],
            ["history.read", "chat.read"],
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
