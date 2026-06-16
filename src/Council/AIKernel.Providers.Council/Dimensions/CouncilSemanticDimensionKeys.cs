namespace AIKernel.Providers.Council.Dimensions;

using AIKernel.Enums.Governance;

/// <summary>
/// [EN] Stable semantic dimension keys emitted by council providers for downstream normalization.
/// [JA] downstream normalization 向けに council Provider が出力する安定した semantic dimension key です。
/// </summary>
public static class CouncilSemanticDimensionKeys
{
    /// <summary>
    /// [EN] Value used when a configured provider did not evaluate a dimension.
    /// [JA] 構成済み Provider が dimension を評価しなかった場合に使用する値です。
    /// </summary>
    public const string NotEvaluated = "not_evaluated";

    /// <summary>
    /// [EN] Logos key for logical consistency material.
    /// [JA] logical consistency material を表す Logos key です。
    /// </summary>
    public const string LogosLogicalConsistency = "logos.logical_consistency";

    /// <summary>
    /// [EN] Logos key for evidence grounding material.
    /// [JA] evidence grounding material を表す Logos key です。
    /// </summary>
    public const string LogosEvidenceGrounding = "logos.evidence_grounding";

    /// <summary>
    /// [EN] Logos key for causal coherence material.
    /// [JA] causal coherence material を表す Logos key です。
    /// </summary>
    public const string LogosCausalCoherence = "logos.causal_coherence";

    /// <summary>
    /// [EN] Ethos key for safety alignment material.
    /// [JA] safety alignment material を表す Ethos key です。
    /// </summary>
    public const string EthosSafetyAlignment = "ethos.safety_alignment";

    /// <summary>
    /// [EN] Ethos key for permission alignment material.
    /// [JA] permission alignment material を表す Ethos key です。
    /// </summary>
    public const string EthosPermissionAlignment = "ethos.permission_alignment";

    /// <summary>
    /// [EN] Ethos key for reversibility material.
    /// [JA] reversibility material を表す Ethos key です。
    /// </summary>
    public const string EthosReversibility = "ethos.reversibility";

    /// <summary>
    /// [EN] Pathos key for context alignment material.
    /// [JA] context alignment material を表す Pathos key です。
    /// </summary>
    public const string PathosContextAlignment = "pathos.context_alignment";

    /// <summary>
    /// [EN] Pathos key for user intent alignment material.
    /// [JA] user intent alignment material を表す Pathos key です。
    /// </summary>
    public const string PathosUserIntentAlignment = "pathos.user_intent_alignment";

    /// <summary>
    /// [EN] Pathos key for impact alignment material.
    /// [JA] impact alignment material を表す Pathos key です。
    /// </summary>
    public const string PathosImpactAlignment = "pathos.impact_alignment";

    /// <summary>
    /// [EN] Gets the minimum Logos semantic dimension keys.
    /// [JA] 最低限の Logos semantic dimension key を取得します。
    /// </summary>
    public static IReadOnlyList<string> LogosMinimumKeys { get; } =
    [
        LogosLogicalConsistency,
        LogosEvidenceGrounding,
        LogosCausalCoherence
    ];

    /// <summary>
    /// [EN] Gets the minimum Ethos semantic dimension keys.
    /// [JA] 最低限の Ethos semantic dimension key を取得します。
    /// </summary>
    public static IReadOnlyList<string> EthosMinimumKeys { get; } =
    [
        EthosSafetyAlignment,
        EthosPermissionAlignment,
        EthosReversibility
    ];

    /// <summary>
    /// [EN] Gets the minimum Pathos semantic dimension keys.
    /// [JA] 最低限の Pathos semantic dimension key を取得します。
    /// </summary>
    public static IReadOnlyList<string> PathosMinimumKeys { get; } =
    [
        PathosContextAlignment,
        PathosUserIntentAlignment,
        PathosImpactAlignment
    ];

    /// <summary>
    /// [EN] Gets the minimum semantic dimension keys for the requested council.
    /// [JA] 指定された council の最低限の semantic dimension key を取得します。
    /// </summary>
    /// <param name="councilKind">EN:  JA: councilKind パラメーターです。
    /// [EN] Council kind.
    /// [JA] council kind です。
    /// </param>
    /// <returns>EN:  JA: 結果を返します。
    /// [EN] Stable minimum key set, or an empty set for unknown councils.
    /// [JA] 安定した最低 key set。不明な council では空 set です。
    /// </returns>
    public static IReadOnlyList<string> GetMinimumKeys(CouncilKind councilKind)
        => councilKind switch
        {
            CouncilKind.Logos => LogosMinimumKeys,
            CouncilKind.Ethos => EthosMinimumKeys,
            CouncilKind.Pathos => PathosMinimumKeys,
            _ => []
        };

    /// <summary>
    /// [EN] Creates a dimension map with all minimum keys set to not evaluated.
    /// [JA] すべての最低 key に not evaluated を設定した dimension map を作成します。
    /// </summary>
    /// <param name="councilKind">EN:  JA: councilKind パラメーターです。
    /// [EN] Council kind.
    /// [JA] council kind です。
    /// </param>
    /// <returns>EN:  JA: 結果を返します。
    /// [EN] Deterministically ordered dimension map for the council.
    /// [JA] council 向けに deterministic order で構成した dimension map です。
    /// </returns>
    public static IReadOnlyDictionary<string, string> CreateNotEvaluatedDimensions(
        CouncilKind councilKind)
        => GetMinimumKeys(councilKind)
            .ToDictionary(key => key, _ => NotEvaluated, StringComparer.Ordinal);
}
