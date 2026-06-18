namespace AIKernel.Providers.Audio.Base;

using AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Structured result returned by audio provider operations.
/// [JA] audio Provider operation が返す構造化 result です。
/// </summary>
public record AudioProviderResult
{
    /// <summary>[EN] Indicates whether the operation succeeded. [JA] operation が成功したかどうかを示します。</summary>
    public bool Succeeded { get; init; }

    /// <summary>[EN] Stable error code when the operation failed. [JA] operation 失敗時の安定した error code です。</summary>
    public string? ErrorCode { get; init; }

    /// <summary>[EN] Human-readable error message when the operation failed. [JA] operation 失敗時の人間可読な error message です。</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>[EN] Structured diagnostics emitted by the operation. [JA] operation が出力する構造化 diagnostic です。</summary>
    public IReadOnlyList<ProviderDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>[EN] Additional deterministic metadata. [JA] 追加の deterministic metadata です。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
