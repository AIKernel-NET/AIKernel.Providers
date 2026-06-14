namespace AIKernel.Providers.Compute;

/// <summary>
/// [EN] Structured validation result for compute buffer references.
/// [JA] compute buffer reference 向けの構造化 validation result です。
/// </summary>
public sealed record ComputeBufferRefValidationResult
{
    /// <summary>[EN] Indicates whether validation succeeded. [JA] validation が成功したかどうかを示します。</summary>
    public bool Succeeded { get; init; }

    /// <summary>[EN] Stable error code when validation failed. [JA] validation 失敗時の安定した error code です。</summary>
    public string? ErrorCode { get; init; }

    /// <summary>[EN] Human-readable error message when validation failed. [JA] validation 失敗時の人間可読な error message です。</summary>
    public string? ErrorMessage { get; init; }
}
