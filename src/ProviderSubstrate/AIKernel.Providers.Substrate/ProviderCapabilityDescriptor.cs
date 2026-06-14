namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Normalized provider capability metadata used by registries and routers.
/// [JA] registry と router が利用する正規化された Provider capability metadata です。
/// </summary>
public sealed record ProviderCapabilityDescriptor
{
    /// <summary>[EN] Provider identifier that owns the capability set. [JA] capability set を所有する Provider 識別子です。</summary>
    public string ProviderId { get; init; } = string.Empty;

    /// <summary>[EN] Capability names exposed by the provider. [JA] Provider が公開する capability 名です。</summary>
    public IReadOnlyList<string> Capabilities { get; init; } = [];

    /// <summary>[EN] Operation names exposed by the provider. [JA] Provider が公開する operation 名です。</summary>
    public IReadOnlyList<string> Operations { get; init; } = [];

    /// <summary>[EN] Data type names exposed by the provider. [JA] Provider が公開する data type 名です。</summary>
    public IReadOnlyList<string> DataTypes { get; init; } = [];

    /// <summary>[EN] Deterministic routing tags. [JA] deterministic routing tag です。</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>[EN] Optional routing priority used only after an explicit fallback is enabled. [JA] 明示的 fallback が有効な場合だけ使う任意の routing priority です。</summary>
    public int Priority { get; init; }

    /// <summary>[EN] Additional deterministic metadata. [JA] 追加の deterministic metadata です。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
