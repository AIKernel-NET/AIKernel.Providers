namespace AIKernel.Providers.Council.Providers;

using AIKernel.Enums.Governance;
using AIKernel.Providers.Council.Envelopes;

/// <summary>
/// [EN] Minimal Ethos semantic evaluation provider that emits semantic material only.
/// [JA] semantic material のみを出力する最小 Ethos semantic evaluation Provider です。
/// </summary>
public sealed class EthosSemanticEvaluationProvider : CouncilSemanticEvaluationProviderBase
{
    /// <summary>
    /// [EN] Initializes an Ethos semantic evaluation provider.
    /// [JA] Ethos semantic evaluation Provider を初期化します。
    /// </summary>
    public EthosSemanticEvaluationProvider()
        : base("ctg.council.ethos", CouncilKind.Ethos)
    {
    }

    /// <inheritdoc />
    protected override ValueTask<CouncilSemanticEvaluationResult> EvaluateCoreAsync(
        CouncilSemanticEvaluationRequest request,
        CancellationToken cancellationToken)
        => ValueTask.FromResult(CreateUnknownResult(request));
}
