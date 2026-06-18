namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Structured missing-provider result for fail-closed routing.
/// [JA] fail-closed routing 向けの構造化 missing-provider result です。
/// </summary>
public sealed record MissingProviderResult
{
    /// <summary>[EN] Requested capability name. [JA] 要求された capability 名です。</summary>
    public string RequiredCapability { get; init; } = string.Empty;

    /// <summary>[EN] Optional preferred provider identifier. [JA] 任意の preferred Provider 識別子です。</summary>
    public string? PreferredProviderId { get; init; }

    /// <summary>[EN] Required routing tags. [JA] 必須 routing tag です。</summary>
    public IReadOnlyList<string> RequiredTags { get; init; } = [];
}
