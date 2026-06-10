using AIKernel.Dtos.Capabilities;
using AIKernel.Enums;

namespace AIKernel.Providers.CudaCompute;

/// <summary>
/// [EN] Contract mapper for CUDA compute provider capabilities.
/// [JA] CUDA compute Provider capability の contract mapper です。
/// </summary>
public static class CudaComputeCapabilityContracts
{
    /// <summary>
    /// [EN] Converts a CUDA provider descriptor into the shared capability module contract.
    /// [JA] CUDA Provider descriptor を共有 capability module contract へ変換します。
    /// </summary>
    public static CapabilityModuleDescriptor ToContract(
        CudaComputeCapabilityDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return new CapabilityModuleDescriptor(
            descriptor.CapabilityId,
            "CUDA Compute Provider",
            CapabilityModuleKind.NativeLibrary,
            CapabilityInvocationMode.NativeAbi,
            GetRequiredMetadataValue(descriptor.Metadata, "version", "0.1.1"),
            GetMetadataValue(descriptor.Metadata, "entry_point", "libtorch_bridge"),
            GetMetadataValue(descriptor.Metadata, "loader_json", null),
            GetMetadataValue(descriptor.Metadata, "artifact_hash", null),
            [
                "tensor.matmul",
                "tensor.softmax",
                "tensor.conv2d",
                "tensor.layernorm"
            ],
            ["native.load", "tensor.compute"],
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
