namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Capability-based provider resolution request.
/// [JA] capability-based な Provider resolution request です。
/// </summary>
public sealed record ProviderResolutionPolicy
{
    /// <summary>[EN] Required capability name. [JA] 必須 capability 名です。</summary>
    public string RequiredCapability { get; init; } = string.Empty;

    /// <summary>[EN] Optional preferred provider identifier. [JA] 任意の preferred Provider 識別子です。</summary>
    public string? PreferredProviderId { get; init; }

    /// <summary>[EN] Required routing tags. [JA] 必須 routing tag です。</summary>
    public IReadOnlyList<string> RequiredTags { get; init; } = [];

    /// <summary>[EN] Runtime hints used by deterministic backend preference. [JA] deterministic backend preference が利用する runtime hint です。</summary>
    public ProviderRuntimeHints RuntimeHints { get; init; } = new();

    /// <summary>[EN] Explicit deterministic fallback policy. [JA] 明示的 deterministic fallback policy です。</summary>
    public DeterministicFallbackPolicy Fallback { get; init; } = new();

    /// <summary>[EN] Additional deterministic metadata. [JA] 追加の deterministic metadata です。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
