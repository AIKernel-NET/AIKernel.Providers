namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Structured duplicate-provider diagnostic for deterministic routing failures.
/// [JA] deterministic routing failure 向けの構造化 duplicate-provider diagnostic です。
/// </summary>
public sealed record DuplicateProviderDiagnostic
{
    /// <summary>[EN] Requested capability name. [JA] 要求された capability 名です。</summary>
    public string RequiredCapability { get; init; } = string.Empty;

    /// <summary>[EN] Matching provider identifiers in deterministic order. [JA] deterministic order の matching Provider 識別子です。</summary>
    public IReadOnlyList<string> ProviderIds { get; init; } = [];
}
