namespace AIKernel.Providers.Council.Concepts;

using AIKernel.Enums.Governance;

/// <summary>
/// [Governance layer - Ethos / エトス]
/// [EN] Concept facade for ethical, safety, and normative council material.
/// [JA] ethical / safety / normative council material の概念 facade です。
/// Old technical name: EthicalCouncilSurface.
/// Do not use this term for DTO, Mapper, Adapter, Serializer, or concrete Provider implementation names.
/// </summary>
public sealed class EthosCouncil
{
    /// <summary>[EN] Council kind represented by this concept. [JA] この concept が表す council kind です。</summary>
    public CouncilKind CouncilKind => CouncilKind.Ethos;

    /// <summary>[EN] Stable semantic responsibility. [JA] 安定した semantic responsibility です。</summary>
    public string Responsibility => "Ethics, safety, and norms";
}

/// <summary>
/// [Governance layer - Pathos / パトス]
/// [EN] Concept facade for risk, anomaly, and danger-signal material.
/// [JA] risk / anomaly / danger signal material の概念 facade です。
/// Old technical name: RiskSignalSurface.
/// Do not use this term for DTO, Mapper, Adapter, Serializer, or concrete Provider implementation names.
/// </summary>
public sealed class PathosSignal
{
    /// <summary>[EN] Council kind represented by this concept. [JA] この concept が表す council kind です。</summary>
    public CouncilKind CouncilKind => CouncilKind.Pathos;

    /// <summary>[EN] Stable semantic responsibility. [JA] 安定した semantic responsibility です。</summary>
    public string Responsibility => "Risk, anomaly, and danger signals";
}

/// <summary>
/// [Governance layer - Logos / ロゴス]
/// [EN] Concept facade for logical consistency and verification material.
/// [JA] logical consistency / verification material の概念 facade です。
/// Old technical name: LogicalVerifierSurface.
/// Do not use this term for DTO, Mapper, Adapter, Serializer, or concrete Provider implementation names.
/// </summary>
public sealed class LogosVerifier
{
    /// <summary>[EN] Council kind represented by this concept. [JA] この concept が表す council kind です。</summary>
    public CouncilKind CouncilKind => CouncilKind.Logos;

    /// <summary>[EN] Stable semantic responsibility. [JA] 安定した semantic responsibility です。</summary>
    public string Responsibility => "Logic, consistency, and verification";
}

/// <summary>
/// [Perception layer - Aisthesis / アイステーシス]
/// [EN] Concept facade for raw perception routing material, not provider routing implementation.
/// [JA] Provider routing implementation ではなく raw perception routing material を表す概念 facade です。
/// Old technical name: ObservationRouter.
/// Do not use this term for DTO, Mapper, Adapter, Serializer, or concrete Provider implementation names.
/// </summary>
public sealed class AisthesisRouter
{
    /// <summary>
    /// [EN] Creates a stable perception route label.
    /// [JA] 安定した perception route label を作成します。
    /// </summary>
    public string Label(string surfaceId)
        => string.IsNullOrWhiteSpace(surfaceId)
            ? "aisthesis.route"
            : $"aisthesis.route.{surfaceId}";
}

/// <summary>
/// [Perception layer - Phantasia / ファンタシア]
/// [EN] Concept facade for scene and internal-world model material.
/// [JA] scene / internal-world model material の概念 facade です。
/// Old technical name: SceneModel.
/// Do not use this term for DTO, ProviderManifest, Mapper, Adapter, Serializer, or concrete Provider implementation names.
/// </summary>
public sealed class PhantasiaScene
{
    /// <summary>
    /// [EN] Creates a stable scene label.
    /// [JA] 安定した scene label を作成します。
    /// </summary>
    public string Label(string sceneId)
        => string.IsNullOrWhiteSpace(sceneId)
            ? "phantasia.scene"
            : $"phantasia.scene.{sceneId}";
}
