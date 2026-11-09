using AIKernel.Dtos.Capabilities;
using AIKernel.Enums;

namespace AIKernel.Providers.LocalLlm;

/// <summary>
/// [EN] Contract mapper for local LLM provider capabilities.
/// [JA] Local LLM Provider capability の contract mapper です。
/// </summary>
public static class LocalLlmCapabilityContracts
{
    /// <summary>
    /// [EN] Converts a local LLM descriptor into the shared capability module contract.
    /// [JA] Local LLM descriptor を共有 capability module contract へ変換します。
    /// </summary>
    public static CapabilityModuleDescriptor ToContract(
        LocalLlmCapabilityDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return new CapabilityModuleDescriptor(
            descriptor.CapabilityId,
            "Local LLM Provider",
            CapabilityModuleKind.ManagedAssembly,
            CapabilityInvocationMode.AssemblyReference,
            GetRequiredMetadataValue(descriptor.Metadata, "version", "0.1.3"),
            descriptor.Runtime,
            GetMetadataValue(descriptor.Metadata, "runtime_uri", null),
            GetMetadataValue(descriptor.Metadata, "artifact_hash", null),
            [
                "chat.local",
                "embedding.local"
            ],
            ["local.process", "llm.local"],
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
