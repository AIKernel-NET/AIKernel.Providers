namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Runtime hints used by deterministic provider resolution.
/// [JA] deterministic Provider resolution が利用する runtime hint です。
/// </summary>
public sealed record ProviderRuntimeHints
{
    /// <summary>[EN] Preferred backend name. [JA] preferred backend 名です。</summary>
    public string? Backend { get; init; }

    /// <summary>[EN] Enables the optional local-over-remote backend preference. [JA] 任意の local-over-remote backend preference を有効にします。</summary>
    public bool PreferLocalBackend { get; init; }

    /// <summary>[EN] Ordered backend names used after the explicit backend hint. [JA] 明示的 backend hint の後に使う順序付き backend 名です。</summary>
    public IReadOnlyList<string> BackendPreferenceOrder { get; init; } = [];

    /// <summary>[EN] Additional deterministic metadata. [JA] 追加の deterministic metadata です。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
