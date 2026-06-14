namespace AIKernel.Providers.Audio.Validation;

using AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Structured result returned by audio format validation.
/// [JA] audio format validation が返す構造化 result です。
/// </summary>
public sealed record AudioFormatValidationResult
{
    /// <summary>[EN] Indicates whether validation succeeded. [JA] validation が成功したかどうかを示します。</summary>
    public bool Succeeded { get; init; }

    /// <summary>[EN] Stable error code when validation failed. [JA] validation 失敗時の安定した error code です。</summary>
    public string? ErrorCode { get; init; }

    /// <summary>[EN] Human-readable error message when validation failed. [JA] validation 失敗時の人間可読な error message です。</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>[EN] Structured diagnostics emitted during validation. [JA] validation 中に出力された構造化 diagnostic です。</summary>
    public IReadOnlyList<ProviderDiagnostic> Diagnostics { get; init; } = [];
}
