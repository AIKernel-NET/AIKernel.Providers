namespace AIKernel.Providers.Council.Providers;

using AIKernel.Enums.Governance;
using AIKernel.Providers.Council.Envelopes;

/// <summary>
/// [EN] Minimal Pathos semantic evaluation provider that emits semantic material only.
/// [JA] semantic material のみを出力する最小 Pathos semantic evaluation Provider です。
/// </summary>
public sealed class PathosSemanticEvaluationProvider : CouncilSemanticEvaluationProviderBase
{
    /// <summary>
    /// [EN] Initializes a Pathos semantic evaluation provider.
    /// [JA] Pathos semantic evaluation Provider を初期化します。
    /// </summary>
    public PathosSemanticEvaluationProvider()
        : base("ctg.council.pathos", CouncilKind.Pathos)
    {
    }

    /// <inheritdoc />
    protected override ValueTask<CouncilSemanticEvaluationResult> EvaluateCoreAsync(
        CouncilSemanticEvaluationRequest request,
        CancellationToken cancellationToken)
        => ValueTask.FromResult(CreateUnknownResult(request));
}
