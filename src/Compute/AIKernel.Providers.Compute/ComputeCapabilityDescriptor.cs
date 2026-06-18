namespace AIKernel.Providers.Compute;

/// <summary>
/// [EN] Backend-neutral compute capability descriptor for provider adapters.
/// [JA] Provider adapter 向けの backend-neutral な compute capability descriptor です。
/// </summary>
public sealed record ComputeCapabilityDescriptor
{
    /// <summary>[EN] Provider identifier. [JA] Provider 識別子です。</summary>
    public string ProviderId { get; init; } = string.Empty;

    /// <summary>[EN] Compute capability names exposed by the provider. [JA] Provider が公開する compute capability 名です。</summary>
    public IReadOnlyList<string> Capabilities { get; init; } = [];

    /// <summary>[EN] Supported dtype names. [JA] 対応する dtype 名です。</summary>
    public IReadOnlyList<string> SupportedDTypes { get; init; } = [];

    /// <summary>[EN] Supported operation names. [JA] 対応する operation 名です。</summary>
    public IReadOnlyList<string> SupportedOperations { get; init; } = [];

    /// <summary>[EN] Additional deterministic metadata. [JA] 追加の deterministic metadata です。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
