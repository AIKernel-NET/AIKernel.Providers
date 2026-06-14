namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Structured validation error emitted while checking a provider manifest.
/// [JA] Provider manifest の検証中に出力される構造化 validation error です。
/// </summary>
public sealed record ProviderManifestValidationError
{
    /// <summary>[EN] Stable validation error code. [JA] 安定した validation error code です。</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>[EN] Human-readable validation error message. [JA] 人間可読な validation error message です。</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>[EN] Manifest field related to the error when known. [JA] error に関連する manifest field です。</summary>
    public string? Field { get; init; }

    /// <summary>[EN] Manifest source path or identifier when available. [JA] 利用可能な場合の manifest source path または identifier です。</summary>
    public string? Source { get; init; }

    /// <summary>[EN] Additional forward-compatible validation metadata. [JA] 将来互換の追加 validation metadata です。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; }
        = new Dictionary<string, string>(StringComparer.Ordinal);
}
