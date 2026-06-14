namespace AIKernel.Providers.Council.Envelopes;

using AIKernel.Enums.Governance;
using AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Result returned by a council semantic evaluation provider.
/// [JA] council semantic evaluation Provider が返す result です。
/// </summary>
public sealed record CouncilSemanticEvaluationResult
{
    /// <summary>[EN] Indicates whether semantic evaluation material was produced. [JA] semantic evaluation material が生成されたかどうかを示します。</summary>
    public bool Succeeded { get; init; }

    /// <summary>[EN] Provider identifier that produced the result. [JA] result を生成した Provider 識別子です。</summary>
    public string ProviderId { get; init; } = string.Empty;

    /// <summary>[EN] Council kind represented by the result. [JA] result が表す council kind です。</summary>
    public CouncilKind CouncilKind { get; init; } = CouncilKind.Unknown;

    /// <summary>[EN] Semantic result payload. [JA] semantic result payload です。</summary>
    public ProviderSemanticResult? SemanticResult { get; init; }

    /// <summary>[EN] Stable error code when evaluation failed. [JA] evaluation 失敗時の安定した error code です。</summary>
    public string? ErrorCode { get; init; }

    /// <summary>[EN] Human-readable error message when evaluation failed. [JA] evaluation 失敗時の人間可読な error message です。</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>[EN] Diagnostics emitted during semantic evaluation. [JA] semantic evaluation 中に出力された diagnostic です。</summary>
    public IReadOnlyList<ProviderDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>[EN] Evidence references emitted during semantic evaluation. [JA] semantic evaluation 中に出力された evidence reference です。</summary>
    public IReadOnlyList<ProviderEvidenceRef> Evidence { get; init; } = [];

    /// <summary>[EN] Additional deterministic metadata. [JA] 追加の deterministic metadata です。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
