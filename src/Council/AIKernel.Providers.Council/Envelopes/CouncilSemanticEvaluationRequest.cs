namespace AIKernel.Providers.Council.Envelopes;

using AIKernel.Dtos.Governance;
using AIKernel.Enums.Governance;

/// <summary>
/// [EN] Request for council semantic evaluation material.
/// [JA] council semantic evaluation material の request です。
/// </summary>
public sealed record CouncilSemanticEvaluationRequest
{
    /// <summary>[EN] Operation identifier supplied by the orchestration layer. [JA] orchestration layer が供給する operation identifier です。</summary>
    public string OperationId { get; init; } = string.Empty;

    /// <summary>[EN] Step identifier supplied by the orchestration layer. [JA] orchestration layer が供給する step identifier です。</summary>
    public string StepId { get; init; } = string.Empty;

    /// <summary>[EN] Council kind requested from the provider. [JA] Provider に要求する council kind です。</summary>
    public CouncilKind CouncilKind { get; init; } = CouncilKind.Unknown;

    /// <summary>[EN] Canon references available to the provider. [JA] Provider が利用できる CanonReference です。</summary>
    public IReadOnlyList<CanonReference> CanonReferences { get; init; } = [];

    /// <summary>[EN] Input facts supplied as semantic material. [JA] semantic material として供給される input fact です。</summary>
    public IReadOnlyDictionary<string, string> InputFacts { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>[EN] Optional correlation identifier. [JA] 任意の correlation identifier です。</summary>
    public string? CorrelationId { get; init; }

    /// <summary>[EN] Optional trace identifier. [JA] 任意の trace identifier です。</summary>
    public string? TraceId { get; init; }

    /// <summary>[EN] Additional deterministic metadata. [JA] 追加の deterministic metadata です。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
