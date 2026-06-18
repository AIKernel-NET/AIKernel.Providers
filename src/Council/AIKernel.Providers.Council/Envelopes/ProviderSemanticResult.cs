namespace AIKernel.Providers.Council.Envelopes;

using AIKernel.Dtos.Governance;
using AIKernel.Enums.Governance;
using AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Semantic material emitted by a council provider before downstream normalization.
/// [JA] downstream normalization 前に council Provider が出力する semantic material です。
/// </summary>
public sealed record ProviderSemanticResult
{
    /// <summary>[EN] Provider identifier that emitted the semantic result. [JA] semantic result を出力した Provider 識別子です。</summary>
    public string ProviderId { get; init; } = string.Empty;

    /// <summary>[EN] Council kind represented by the semantic result. [JA] semantic result が表す council kind です。</summary>
    public CouncilKind CouncilKind { get; init; } = CouncilKind.Unknown;

    /// <summary>[EN] Semantic evaluation status before Control-side normalization. [JA] Control 側の normalization 前の semantic evaluation status です。</summary>
    public SemanticEvaluationStatus Status { get; init; } = SemanticEvaluationStatus.Unknown;

    /// <summary>[EN] Discrete vote value proposed by the provider. [JA] Provider が提案する discrete vote value です。</summary>
    public CouncilVoteValue VoteValue { get; init; } = CouncilVoteValue.Unknown;

    /// <summary>[EN] Canonical discrete vote value proposed by the provider. [JA] Provider が提案する正準 discrete vote value です。</summary>
    public CouncilVoteValue ProposedVoteValue
    {
        get => VoteValue;
        init => VoteValue = value;
    }

    /// <summary>[EN] Optional observed confidence carrier for diagnostics. [JA] diagnostics 向けの任意の observed confidence carrier です。</summary>
    public double? Confidence { get; init; }

    /// <summary>[EN] Optional observed risk score carrier for diagnostics. [JA] diagnostics 向けの任意の observed risk score carrier です。</summary>
    public double? RiskScore { get; init; }

    /// <summary>[EN] Optional reason text supplied by the provider. [JA] Provider が供給する任意の reason text です。</summary>
    public string? Reason { get; init; }

    /// <summary>[EN] Canonical rationale text supplied by the provider. [JA] Provider が供給する正準 rationale text です。</summary>
    public string? Rationale
    {
        get => Reason;
        init => Reason = value;
    }

    /// <summary>[EN] Canon references supporting the semantic result. [JA] semantic result を支える CanonReference です。</summary>
    public IReadOnlyList<CanonReference> CanonReferences { get; init; } = [];

    /// <summary>[EN] Semantic dimensions for Control-side normalization; these values are not Gate input. [JA] Control 側の normalization 向け semantic dimension です。これらの値は Gate input ではありません。</summary>
    public IReadOnlyDictionary<string, string> Dimensions { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>[EN] Diagnostics emitted by the provider. [JA] Provider が出力する diagnostic です。</summary>
    public IReadOnlyList<ProviderDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>[EN] Evidence references emitted by the provider. [JA] Provider が出力する evidence reference です。</summary>
    public IReadOnlyList<ProviderEvidenceRef> Evidence { get; init; } = [];

    /// <summary>[EN] Canonical evidence references emitted by the provider. [JA] Provider が出力する正準 evidence reference です。</summary>
    public IReadOnlyList<ProviderEvidenceRef> EvidenceRefs
    {
        get => Evidence;
        init => Evidence = value;
    }

    /// <summary>[EN] Additional deterministic metadata for downstream orchestration. [JA] downstream orchestration 向けの追加 deterministic metadata です。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
