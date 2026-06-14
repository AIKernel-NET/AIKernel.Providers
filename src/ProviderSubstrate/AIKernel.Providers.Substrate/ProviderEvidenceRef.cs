namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Reference to provider-side evidence without embedding external artifacts.
/// [JA] 外部 artifact を埋め込まずに Provider 側 evidence を参照する型です。
/// </summary>
public sealed record ProviderEvidenceRef
{
    /// <summary>[EN] Stable evidence identifier. [JA] 安定した evidence identifier です。</summary>
    public string EvidenceId { get; init; } = string.Empty;

    /// <summary>[EN] Evidence kind such as manifest, descriptor, log, or artifact. [JA] manifest、descriptor、log、artifact などの evidence kind です。</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>[EN] Optional URI for the evidence carrier. [JA] evidence carrier の任意 URI です。</summary>
    public string? Uri { get; init; }

    /// <summary>[EN] Optional content hash carried by the descriptor. [JA] descriptor が保持する任意の content hash です。</summary>
    public string? ContentHash { get; init; }

    /// <summary>[EN] Additional deterministic metadata. [JA] 追加の deterministic metadata です。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
