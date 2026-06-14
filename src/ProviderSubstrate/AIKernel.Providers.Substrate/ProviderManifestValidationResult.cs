namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Result returned by provider manifest validation.
/// [JA] Provider manifest validation が返す result です。
/// </summary>
public sealed record ProviderManifestValidationResult
{
    /// <summary>[EN] Indicates whether validation succeeded. [JA] validation が成功したかどうかを示します。</summary>
    public bool Succeeded { get; init; }

    /// <summary>[EN] Stable error code when validation failed. [JA] validation 失敗時の安定した error code です。</summary>
    public string? ErrorCode { get; init; }

    /// <summary>[EN] Human-readable error message when validation failed. [JA] validation 失敗時の人間可読な error message です。</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>[EN] Structured validation errors for strict consumers. [JA] strict consumer 向けの構造化 validation error です。</summary>
    public IReadOnlyList<ProviderManifestValidationError> Errors { get; init; } = [];

    /// <summary>[EN] Structured diagnostics emitted during validation. [JA] validation 中に出力された構造化 diagnostic です。</summary>
    public IReadOnlyList<ProviderDiagnostic> Diagnostics { get; init; } = [];
}
