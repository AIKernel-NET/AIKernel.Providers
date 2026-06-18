namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Selected backend metadata returned by deterministic provider resolution.
/// [JA] deterministic Provider resolution が返す selected backend metadata です。
/// </summary>
public sealed record ProviderBackendSelection
{
    /// <summary>[EN] Selected backend name. [JA] 選択された backend 名です。</summary>
    public string BackendName { get; init; } = string.Empty;

    /// <summary>[EN] Backend kind. [JA] backend kind です。</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>[EN] Effective backend rank used for resolution. [JA] resolution で使われた effective backend rank です。</summary>
    public int EffectiveRank { get; init; }

    /// <summary>[EN] Backend metadata projected to string values. [JA] string 値へ射影された backend metadata です。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
