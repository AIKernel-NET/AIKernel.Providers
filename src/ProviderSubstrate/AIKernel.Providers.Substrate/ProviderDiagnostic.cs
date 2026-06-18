namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Structured diagnostic emitted by provider substrate operations.
/// [JA] Provider substrate operation が出力する構造化 diagnostic です。
/// </summary>
public sealed record ProviderDiagnostic
{
    /// <summary>[EN] Stable diagnostic code. [JA] 安定した diagnostic code です。</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>[EN] Human-readable diagnostic message. [JA] 人間可読な diagnostic message です。</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>[EN] Diagnostic severity label. [JA] diagnostic severity label です。</summary>
    public string Severity { get; init; } = "Information";

    /// <summary>[EN] Optional source path or manifest identifier. [JA] 任意の source path または manifest identifier です。</summary>
    public string? Source { get; init; }

    /// <summary>[EN] Additional deterministic metadata. [JA] 追加の deterministic metadata です。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
