namespace AIKernel.Providers.Council.Providers;

using AIKernel.Enums.Governance;
using AIKernel.Providers.Council.Contracts;
using AIKernel.Providers.Council.Dimensions;
using AIKernel.Providers.Council.Envelopes;
using AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Base class for council semantic evaluation providers.
/// [JA] council semantic evaluation Provider の base class です。
/// [EN] Ethos, Pathos, and Logos are CTG council concepts here; this provider base emits semantic material only and never creates GateInput or Gate decisions.
/// [JA] ここでの Ethos / Pathos / Logos は CTG council concept です。この Provider base は semantic material のみを出力し、GateInput や Gate decision は生成しません。
/// Old technical name: CouncilSemanticEvaluationProviderBase.
/// Do not use council concept terms for DTO, Mapper, Adapter, Serializer, ProviderManifest, or concrete non-council Provider implementation names.
/// </summary>
public abstract class CouncilSemanticEvaluationProviderBase : ICouncilSemanticEvaluationProvider
{
    /// <summary>
    /// [EN] Initializes the provider with stable identity and council metadata.
    /// [JA] 安定した identity と council metadata で Provider を初期化します。
    /// </summary>
    /// <param name="providerId">EN:  JA: providerId パラメーターです。
    /// [EN] Provider identifier.
    /// [JA] Provider 識別子です。
    /// </param>
    /// <param name="councilKind">EN:  JA: councilKind パラメーターです。
    /// [EN] Council kind handled by the provider.
    /// [JA] Provider が扱う council kind です。
    /// </param>
    protected CouncilSemanticEvaluationProviderBase(
        string providerId,
        CouncilKind councilKind)
    {
        ProviderId = string.IsNullOrWhiteSpace(providerId)
            ? throw new ArgumentException("Provider id is required.", nameof(providerId))
            : providerId;
        CouncilKind = councilKind;
    }

    /// <summary>
    /// [EN] Gets the provider identifier.
    /// [JA] Provider 識別子を取得します。
    /// </summary>
    public string ProviderId { get; }

    /// <summary>
    /// [EN] Gets the council kind handled by this provider.
    /// [JA] この Provider が扱う council kind を取得します。
    /// </summary>
    public CouncilKind CouncilKind { get; }

    /// <summary>
    /// [EN] Evaluates semantic material for the requested council.
    /// [JA] 要求された council の semantic material を評価します。
    /// </summary>
    /// <param name="request">EN:  JA: request パラメーターです。
    /// [EN] Council semantic evaluation request.
    /// [JA] council semantic evaluation request です。
    /// </param>
    /// <param name="cancellationToken">EN:  JA: cancellationToken パラメーターです。
    /// [EN] Cancellation token.
    /// [JA] cancellation token です。
    /// </param>
    /// <returns>EN:  JA: 結果を返します。
    /// [EN] Council semantic evaluation result.
    /// [JA] council semantic evaluation result です。
    /// </returns>
    public ValueTask<CouncilSemanticEvaluationResult> EvaluateAsync(
        CouncilSemanticEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        if (request.CouncilKind != CouncilKind.Unknown && request.CouncilKind != CouncilKind)
        {
            return ValueTask.FromResult(Failure(
                "COUNCIL_PROVIDER_KIND_MISMATCH",
                "Council provider received a request for a different council kind.",
                request));
        }

        return EvaluateCoreAsync(request, cancellationToken);
    }

    /// <summary>
    /// [EN] Evaluates provider-specific semantic material.
    /// [JA] Provider 固有の semantic material を評価します。
    /// </summary>
    /// <param name="request">EN:  JA: request パラメーターです。
    /// [EN] Council semantic evaluation request.
    /// [JA] council semantic evaluation request です。
    /// </param>
    /// <param name="cancellationToken">EN:  JA: cancellationToken パラメーターです。
    /// [EN] Cancellation token.
    /// [JA] cancellation token です。
    /// </param>
    /// <returns>EN:  JA: 結果を返します。
    /// [EN] Council semantic evaluation result.
    /// [JA] council semantic evaluation result です。
    /// </returns>
    protected abstract ValueTask<CouncilSemanticEvaluationResult> EvaluateCoreAsync(
        CouncilSemanticEvaluationRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// [EN] Creates an Unknown vote semantic result for a configured but non-evaluating provider.
    /// [JA] 構成済みだが評価を行わない Provider 用の Unknown vote semantic result を作成します。
    /// </summary>
    /// <param name="request">EN:  JA: request パラメーターです。
    /// [EN] Council semantic evaluation request.
    /// [JA] council semantic evaluation request です。
    /// </param>
    /// <returns>EN:  JA: 結果を返します。
    /// [EN] Unknown vote semantic evaluation result.
    /// [JA] Unknown vote の semantic evaluation result です。
    /// </returns>
    protected CouncilSemanticEvaluationResult CreateUnknownResult(
        CouncilSemanticEvaluationRequest request)
    {
        var diagnostic = new ProviderDiagnostic
        {
            Code = "COUNCIL_PROVIDER_SEMANTIC_STUB",
            Message = "Council semantic provider returned an Unknown vote because no semantic backend is configured.",
            Severity = "Warning",
            Source = ProviderId
        };
        var semantic = new ProviderSemanticResult
        {
            ProviderId = ProviderId,
            CouncilKind = CouncilKind,
            Status = SemanticEvaluationStatus.Inconclusive,
            ProposedVoteValue = CouncilVoteValue.Unknown,
            Reason = "No semantic backend is configured.",
            CanonReferences = request.CanonReferences,
            Dimensions = CouncilSemanticDimensionKeys.CreateNotEvaluatedDimensions(CouncilKind),
            Diagnostics = [diagnostic],
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ctg.council.provider_id"] = ProviderId,
                ["ctg.council.kind"] = CouncilKind.ToString()
            }
        };

        return new CouncilSemanticEvaluationResult
        {
            Succeeded = true,
            ProviderId = ProviderId,
            CouncilKind = CouncilKind,
            SemanticResult = semantic,
            Diagnostics = [diagnostic],
            Metadata = semantic.Metadata
        };
    }

    private CouncilSemanticEvaluationResult Failure(
        string code,
        string message,
        CouncilSemanticEvaluationRequest request)
        => new()
        {
            Succeeded = false,
            ProviderId = ProviderId,
            CouncilKind = CouncilKind,
            ErrorCode = code,
            ErrorMessage = message,
            Diagnostics =
            [
                new ProviderDiagnostic
                {
                    Code = code,
                    Message = message,
                    Severity = "Error",
                    Source = ProviderId,
                    Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["requestedCouncil"] = request.CouncilKind.ToString(),
                        ["providerCouncil"] = CouncilKind.ToString()
                    }
                }
            ]
        };
}
