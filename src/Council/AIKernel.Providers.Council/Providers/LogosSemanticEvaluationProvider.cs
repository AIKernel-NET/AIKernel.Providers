namespace AIKernel.Providers.Council.Providers;

using AIKernel.Enums.Governance;
using AIKernel.Providers.Council.Envelopes;

/// <summary>
/// [EN] Minimal Logos semantic evaluation provider that emits semantic material only.
/// [JA] semantic material のみを出力する最小 Logos semantic evaluation Provider です。
/// </summary>
public sealed class LogosSemanticEvaluationProvider : CouncilSemanticEvaluationProviderBase
{
    /// <summary>
    /// [EN] Initializes a Logos semantic evaluation provider.
    /// [JA] Logos semantic evaluation Provider を初期化します。
    /// </summary>
    public LogosSemanticEvaluationProvider()
        : base("ctg.council.logos", CouncilKind.Logos)
    {
    }

    /// <inheritdoc />
    protected override ValueTask<CouncilSemanticEvaluationResult> EvaluateCoreAsync(
        CouncilSemanticEvaluationRequest request,
        CancellationToken cancellationToken)
        => ValueTask.FromResult(CreateUnknownResult(request));
}
