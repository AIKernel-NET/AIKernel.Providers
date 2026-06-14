namespace AIKernel.Providers.CudaCompute;

/// <summary>
/// [EN] Descriptor for a runtime-configurable CUDA backend boundary.
/// [JA] runtime-configurable な CUDA backend 境界の descriptor です。
/// </summary>
public sealed record CudaBackendDescriptor
{
    /// <summary>[EN] Provider identifier. [JA] Provider 識別子です。</summary>
    public string ProviderId { get; init; } = string.Empty;

    /// <summary>[EN] Runtime-configurable backend name. [JA] runtime-configurable backend 名です。</summary>
    public string BackendName { get; init; } = string.Empty;

    /// <summary>[EN] Canonical runtime-configurable backend identifier. [JA] runtime-configurable backend の正準識別子です。</summary>
    public string BackendId
    {
        get => BackendName;
        init => BackendName = value;
    }

    /// <summary>[EN] Optional backend version. [JA] 任意の backend version です。</summary>
    public string? BackendVersion { get; init; }

    /// <summary>[EN] Optional dedicated backend package identifier. [JA] 任意の dedicated backend package 識別子です。</summary>
    public string? PackageId { get; init; }

    /// <summary>[EN] Device profile requested by the descriptor. [JA] descriptor が要求する device profile です。</summary>
    public string DeviceProfile { get; init; } = string.Empty;

    /// <summary>[EN] Supported compute capability names advertised by the backend. [JA] backend が公開する supported compute capability 名です。</summary>
    public IReadOnlyList<string> SupportedComputeCapabilities { get; init; } = [];

    /// <summary>[EN] Native module descriptor carried by reference only. [JA] 参照のみで保持する native module descriptor です。</summary>
    public NativeModuleDescriptor NativeModule { get; init; } = new();

    /// <summary>[EN] Compute operations advertised by this backend boundary. [JA] この backend 境界が公開する compute operation です。</summary>
    public IReadOnlyList<string> Operations { get; init; } = [];

    /// <summary>[EN] Canonical compute operations advertised by this backend boundary. [JA] この backend 境界が公開する正準 compute operation です。</summary>
    public IReadOnlyList<string> SupportedOps
    {
        get => Operations;
        init => Operations = value;
    }

    /// <summary>[EN] Optional maximum device memory in bytes. [JA] 任意の最大 device memory byte 数です。</summary>
    public long? MaxDeviceMemoryBytes { get; init; }

    /// <summary>[EN] Additional deterministic metadata. [JA] 追加の deterministic metadata です。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
