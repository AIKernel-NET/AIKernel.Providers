namespace AIKernel.Providers.CudaCompute;

/// <summary>
/// [EN] Descriptor-only policy for resolving a CUDA backend boundary.
/// [JA] CUDA backend 境界を解決する descriptor-only policy です。
/// </summary>
public sealed record CudaBackendResolutionPolicy
{
    /// <summary>[EN] Required backend name. [JA] 必須 backend 名です。</summary>
    public string RequiredBackend { get; init; } = string.Empty;

    /// <summary>[EN] Required device profile. [JA] 必須 device profile です。</summary>
    public string RequiredDeviceProfile { get; init; } = string.Empty;

    /// <summary>[EN] Required operation names. [JA] 必須 operation 名です。</summary>
    public IReadOnlyList<string> RequiredOperations { get; init; } = [];

    /// <summary>[EN] Additional deterministic metadata. [JA] 追加の deterministic metadata です。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
