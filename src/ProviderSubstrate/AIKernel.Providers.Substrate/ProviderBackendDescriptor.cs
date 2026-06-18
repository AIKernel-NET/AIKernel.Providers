namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Backend descriptor carried by a provider manifest for deterministic resolution.
/// [JA] deterministic resolution 向けに Provider manifest が保持する backend descriptor です。
/// </summary>
public sealed record ProviderBackendDescriptor
{
    /// <summary>[EN] Backend name. [JA] backend 名です。</summary>
    public string BackendName { get; init; } = string.Empty;

    /// <summary>[EN] Backend kind such as local, remote, or descriptor. [JA] local、remote、descriptor などの backend kind です。</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>[EN] Backend rank; lower values are preferred. [JA] backend rank です。小さい値を優先します。</summary>
    public int Rank { get; init; }

    /// <summary>[EN] Capability names exposed by this backend. [JA] この backend が公開する capability 名です。</summary>
    public IReadOnlyList<string> Capabilities { get; init; } = [];

    /// <summary>[EN] Deterministic routing tags exposed by this backend. [JA] この backend が公開する deterministic routing tag です。</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>[EN] Backend metadata projected to string values. [JA] string 値へ射影された backend metadata です。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
