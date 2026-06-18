namespace AIKernel.Providers.Perception;

/// <summary>
/// [EN] Describes provider-neutral perception capabilities for manifest and router integration.
/// [JA] manifest と router 統合のための provider-neutral な perception capability を記述します。
/// </summary>
public sealed record PerceptionCapabilityDescriptor
{
    /// <summary>[EN] Gets the provider identifier. [JA] provider 識別子を取得します。</summary>
    public string ProviderId { get; init; } = string.Empty;

    /// <summary>[EN] Gets supported perception modalities. [JA] 対応する perception modality を取得します。</summary>
    public IReadOnlyList<string> Modalities { get; init; } = [];

    /// <summary>[EN] Gets supported signal kinds. [JA] 対応する signal kind を取得します。</summary>
    public IReadOnlyList<string> SignalKinds { get; init; } = [];

    /// <summary>[EN] Gets whether zero-copy surface references are supported by the provider boundary. [JA] provider 境界で zero-copy surface 参照を扱えるかどうかを取得します。</summary>
    public bool SupportsZeroCopySurface { get; init; }

    /// <summary>[EN] Gets whether auditory perception is supported. [JA] auditory perception が対応しているかどうかを取得します。</summary>
    public bool SupportsAuditoryPerception { get; init; }

    /// <summary>[EN] Gets whether spatial cognition composition is supported. [JA] spatial cognition composition が対応しているかどうかを取得します。</summary>
    public bool SupportsSpatialCognition { get; init; }

    /// <summary>[EN] Gets deterministic provider metadata. [JA] deterministic provider metadata を取得します。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
