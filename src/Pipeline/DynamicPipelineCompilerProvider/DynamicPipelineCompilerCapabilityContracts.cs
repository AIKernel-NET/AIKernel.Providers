using AIKernel.Dtos.Capabilities;
using AIKernel.Enums;

namespace AIKernel.Providers.DynamicPipelineCompiler;

/// <summary>
/// [EN] Contract mapper for dynamic pipeline compiler provider capabilities.
/// [JA] Dynamic Pipeline Compiler Provider capability の contract mapper です。
/// </summary>
public static class DynamicPipelineCompilerCapabilityContracts
{
    /// <summary>
    /// [EN] Converts a dynamic pipeline descriptor into the shared capability module contract.
    /// [JA] Dynamic Pipeline descriptor を共有 capability module contract へ変換します。
    /// </summary>
    public static CapabilityModuleDescriptor ToContract(
        DynamicPipelineCompilerCapabilityDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return new CapabilityModuleDescriptor(
            descriptor.CapabilityId,
            "Dynamic Pipeline Compiler Provider",
            CapabilityModuleKind.ManagedAssembly,
            CapabilityInvocationMode.AssemblyReference,
            GetMetadataValue(descriptor.Metadata, "version", "0.1.2"),
            "AIKernel.Providers.DynamicPipelineCompiler",
            null,
            null,
            [
                "pipeline.compile",
                "pipeline.execute",
                "pipeline.validate"
            ],
            ["dsl.read", "capability.register"],
            descriptor.Metadata);
    }

    private static string GetMetadataValue(
        IReadOnlyDictionary<string, string> metadata,
        string key,
        string fallback)
        => metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
}
