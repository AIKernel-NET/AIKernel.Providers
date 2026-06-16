namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Generic provider evaluation envelope for structured success and failure projection.
/// [JA] 構造化された success / failure projection 向けの generic Provider evaluation envelope です。
/// </summary>
/// <typeparam name="T">EN:  JA: T 型パラメーターです。
/// [EN] Payload type.
/// [JA] payload type です。
/// </typeparam>
public sealed record ProviderEvaluationEnvelope<T>
{
    /// <summary>[EN] Indicates whether evaluation succeeded. [JA] evaluation が成功したかどうかを示します。</summary>
    public bool Succeeded { get; init; }

    /// <summary>[EN] Evaluation payload when successful. [JA] 成功時の evaluation payload です。</summary>
    public T? Value { get; init; }

    /// <summary>[EN] Stable error code when evaluation failed. [JA] evaluation 失敗時の安定した error code です。</summary>
    public string? ErrorCode { get; init; }

    /// <summary>[EN] Human-readable error message when evaluation failed. [JA] evaluation 失敗時の人間可読な error message です。</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>[EN] Structured diagnostics emitted during evaluation. [JA] evaluation 中に出力された構造化 diagnostic です。</summary>
    public IReadOnlyList<ProviderDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>[EN] Evidence references associated with the payload. [JA] payload に紐づく evidence reference です。</summary>
    public IReadOnlyList<ProviderEvidenceRef> Evidence { get; init; } = [];
}
