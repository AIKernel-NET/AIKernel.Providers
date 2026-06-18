namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Deterministic provider resolution request.
/// [JA] deterministic Provider resolution request です。
/// </summary>
public sealed record ProviderResolutionRequest
{
    /// <summary>[EN] Provider resolution mode. [JA] Provider resolution mode です。</summary>
    public ProviderResolutionMode Mode { get; init; } = ProviderResolutionMode.Strict;

    /// <summary>[EN] Capability resolution policy. [JA] capability resolution policy です。</summary>
    public ProviderResolutionPolicy Policy { get; init; } = new();

    /// <summary>[EN] Additional deterministic metadata. [JA] 追加の deterministic metadata です。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
