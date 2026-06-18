namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Explicit opt-in fallback policy for deterministic provider routing.
/// [JA] deterministic Provider routing 向けの明示的 opt-in fallback policy です。
/// </summary>
public sealed record DeterministicFallbackPolicy
{
    /// <summary>[EN] Indicates whether fallback routing is enabled. [JA] fallback routing が有効かどうかを示します。</summary>
    public bool Enabled { get; init; }

    /// <summary>[EN] Ordered fallback provider identifiers. [JA] 順序付き fallback Provider 識別子です。</summary>
    public IReadOnlyList<string> ProviderIds { get; init; } = [];
}
