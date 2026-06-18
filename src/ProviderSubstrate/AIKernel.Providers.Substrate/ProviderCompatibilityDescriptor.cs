namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Compatibility metadata declared by a provider manifest.
/// [JA] Provider manifest が宣言する compatibility metadata です。
/// </summary>
public sealed record ProviderCompatibilityDescriptor
{
    /// <summary>[EN] Minimum AIKernel contract version. [JA] 最小 AIKernel contract version です。</summary>
    public string? MinimumAIKernelVersion { get; init; }

    /// <summary>[EN] Maximum AIKernel contract version. [JA] 最大 AIKernel contract version です。</summary>
    public string? MaximumAIKernelVersion { get; init; }

    /// <summary>[EN] Supported target framework names. [JA] 対応する target framework 名です。</summary>
    public IReadOnlyList<string> TargetFrameworks { get; init; } = [];

    /// <summary>[EN] Additional deterministic metadata. [JA] 追加の deterministic metadata です。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
