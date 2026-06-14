namespace AIKernel.Providers.Council.Envelopes;

/// <summary>
/// [EN] Status of semantic evaluation before Control-side vote normalization.
/// [JA] Control 側の vote normalization 前の semantic evaluation status です。
/// </summary>
public enum SemanticEvaluationStatus
{
    /// <summary>[EN] Unknown status; callers should fail closed. [JA] 不明な status です。caller は fail closed してください。</summary>
    Unknown = 0,

    /// <summary>[EN] Evaluation completed and produced semantic material. [JA] evaluation が完了し semantic material を生成しました。</summary>
    Evaluated = 1,

    /// <summary>[EN] Evaluation does not apply to the request. [JA] evaluation が request に適用されません。</summary>
    NotApplicable = 2,

    /// <summary>[EN] Evaluation did not reach a conclusive semantic result. [JA] evaluation が確定的な semantic result に到達しませんでした。</summary>
    Inconclusive = 3,

    /// <summary>[EN] Evaluation failed with a structured error. [JA] evaluation が構造化 error で失敗しました。</summary>
    Failed = 4,

    /// <summary>[EN] Evaluation timed out. [JA] evaluation が timeout しました。</summary>
    Timeout = 5,

    /// <summary>[EN] Provider was unavailable during semantic evaluation. [JA] semantic evaluation 中に Provider が unavailable でした。</summary>
    ProviderUnavailable = 6,

    /// <summary>[EN] Evaluation completed and produced semantic material. [JA] evaluation が完了し semantic material を生成しました。</summary>
    [Obsolete("Use Evaluated.")]
    Completed = Evaluated
}
