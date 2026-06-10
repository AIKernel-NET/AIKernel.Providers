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
    public string Version { get; init; } = "0.1.1";

    /// <summary>[EN] Native CUDA device profile. [JA] Native CUDA device profile です。</summary>
    public string DeviceProfile { get; init; } = "cuda13";

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
            ["device_profile"] = DeviceProfile,
            ["entry_point"] = EntryPoint,
            ["version"] = Version
        };

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
