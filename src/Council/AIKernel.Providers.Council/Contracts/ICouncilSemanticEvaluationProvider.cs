namespace AIKernel.Providers.Council.Contracts;

using AIKernel.Enums.Governance;
using AIKernel.Providers.Council.Envelopes;

/// <summary>
/// [EN] Provides council semantic evaluation material for downstream orchestration.
/// [JA] downstream orchestration 向けの council semantic evaluation material を提供します。
/// </summary>
public interface ICouncilSemanticEvaluationProvider
{
    /// <summary>
    /// [EN] Gets the provider identifier.
    /// [JA] Provider 識別子を取得します。
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// [EN] Gets the council kind handled by this provider.
    /// [JA] この Provider が扱う council kind を取得します。
    /// </summary>
    CouncilKind CouncilKind { get; }

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
    ValueTask<CouncilSemanticEvaluationResult> EvaluateAsync(
        CouncilSemanticEvaluationRequest request,
        CancellationToken cancellationToken);
}
