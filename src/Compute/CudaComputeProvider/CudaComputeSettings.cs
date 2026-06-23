using AIKernel.Dtos.Gpu;

namespace AIKernel.Providers.CudaCompute;

/// <summary>
/// [EN] Settings for the CUDA compute provider boundary.
/// [JA] CUDA compute Provider 境界の設定です。
/// </summary>
public sealed record CudaComputeSettings
{
    /// <summary>[EN] Provider id. [JA] Provider id です。</summary>
    public string ProviderId { get; init; } = "providers.cuda";

    /// <summary>[EN] Human-readable provider name. [JA] 人間可読な Provider 名です。</summary>
    public string Name { get; init; } = "CUDA Compute Provider";

    /// <summary>[EN] Provider contract version. [JA] Provider 契約 version です。</summary>
    public string Version { get; init; } = "0.1.3";

    /// <summary>[EN] Native CUDA device profile. [JA] Native CUDA device profile です。</summary>
    public string DeviceProfile { get; init; } = "cuda13";

    /// <summary>[EN] Runtime-configurable backend name. [JA] runtime-configurable backend 名です。</summary>
    public string BackendName { get; init; } = "cuda13.0";

    /// <summary>[EN] Canonical runtime-configurable backend identifier. [JA] runtime-configurable backend の正準識別子です。</summary>
    public string BackendId
    {
        get => BackendName;
        init => BackendName = value;
    }

    /// <summary>[EN] Optional backend version. [JA] 任意の backend version です。</summary>
    public string? BackendVersion { get; init; } = "13.0";

    /// <summary>[EN] Optional dedicated backend package identifier. [JA] 任意の dedicated backend package 識別子です。</summary>
    public string? PackageId { get; init; } = "aikernel-cuda13";

    /// <summary>[EN] Native module identifier. [JA] native module 識別子です。</summary>
    public string ModuleId { get; init; } = "libtorch_bridge";

    /// <summary>[EN] Optional native module ABI version. [JA] 任意の native module ABI version です。</summary>
    public string? AbiVersion { get; init; } = "1.0";

    /// <summary>[EN] Optional maximum device memory in bytes. [JA] 任意の最大 device memory byte 数です。</summary>
    public long? MaxDeviceMemoryBytes { get; init; }

    /// <summary>[EN] Supported compute capability names advertised by the descriptor. [JA] descriptor が公開する supported compute capability 名です。</summary>
    public IReadOnlyList<string> SupportedComputeCapabilities { get; init; } = [];

    /// <summary>[EN] Descriptor URI for the native module boundary. [JA] native module 境界の descriptor URI です。</summary>
    public string NativeModuleRef { get; init; } = "aikernel-cuda://cuda13.0/modules/libtorch_bridge";

    /// <summary>[EN] Native entry point name. [JA] Native entry point 名です。</summary>
    public string EntryPoint { get; init; } = "libtorch_bridge";

    /// <summary>[EN] Optional loader manifest URI. [JA] 任意の loader manifest URI です。</summary>
    public string? LoaderJson { get; init; } = "rom://providers/cuda/loader.json";

    /// <summary>[EN] Optional native artifact hash. [JA] 任意の native artifact hash です。</summary>
    public string? ArtifactHash { get; init; }

    /// <summary>[EN] Returns deterministic metadata for capability export. [JA] capability export 用の決定論的 metadata を返します。</summary>
    public IReadOnlyDictionary<string, string> ToMetadata()
    {
        var metadata = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            [GpuProviderMetadataKeys.AdapterProfile] = DeviceProfile,
            [GpuProviderMetadataKeys.AotCompilerHooks] = "planned-gpu-native-execution",
            [GpuProviderMetadataKeys.Backend] = BackendName,
            ["backend_id"] = BackendId,
            ["device_profile"] = DeviceProfile,
            [GpuProviderMetadataKeys.DeterministicFrameSampling] = "host-frame-token-sample-ticks",
            ["entry_point"] = EntryPoint,
            [GpuProviderMetadataKeys.Fallback] = "fail-closed",
            [GpuProviderMetadataKeys.GpuBypass] = "native-cuda-buffer-dispatch",
            [GpuProviderMetadataKeys.GpuBackend] = AIKernel.Enums.GpuBackend.Cuda.ToString(),
            [GpuProviderMetadataKeys.GpuCapabilities] = (
                AIKernel.Enums.GpuProviderCapabilities.SupportsCompute |
                AIKernel.Enums.GpuProviderCapabilities.SupportsNativeValidation |
                AIKernel.Enums.GpuProviderCapabilities.SupportsFrameDiagnostics).ToString(),
            ["module_id"] = ModuleId,
            ["native_module_ref"] = NativeModuleRef,
            [GpuProviderMetadataKeys.NativeJsBridge] = "not-required-native-provider",
            [GpuProviderMetadataKeys.PassBridge] = "native-abi",
            [GpuProviderMetadataKeys.ProviderFamily] = "aikernel.gpu.rev3",
            [GpuProviderMetadataKeys.ProviderRole] = "cuda13-native-compute",
            [GpuProviderMetadataKeys.RawCaptureSource] = "none",
            [GpuProviderMetadataKeys.Rev3] = "true",
            [GpuProviderMetadataKeys.Version] = Version,
            [GpuProviderMetadataKeys.ZeroCopyBufferHandling] = "native-cuda-device-buffer"
        };

        if (!string.IsNullOrWhiteSpace(AbiVersion))
        {
            metadata["abi_version"] = AbiVersion;
        }

        if (!string.IsNullOrWhiteSpace(BackendVersion))
        {
            metadata["backend_version"] = BackendVersion;
        }

        if (!string.IsNullOrWhiteSpace(PackageId))
        {
            metadata["package_id"] = PackageId;
        }

        if (MaxDeviceMemoryBytes is { } maxDeviceMemoryBytes)
        {
            metadata["max_device_memory_bytes"] = maxDeviceMemoryBytes.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(LoaderJson))
        {
            metadata["loader_json"] = LoaderJson;
        }

        if (!string.IsNullOrWhiteSpace(ArtifactHash))
        {
            metadata["artifact_hash"] = ArtifactHash;
        }

        return metadata;
    }
}
