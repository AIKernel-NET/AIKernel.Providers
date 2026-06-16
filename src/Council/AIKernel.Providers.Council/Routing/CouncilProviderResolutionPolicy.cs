namespace AIKernel.Providers.Council.Routing;

using AIKernel.Enums.Governance;
using AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Creates substrate routing policies for council semantic providers.
/// [JA] council semantic Provider 向けの substrate routing policy を作成します。
/// </summary>
public sealed class CouncilProviderResolutionPolicy
{
    /// <summary>
    /// [EN] Creates a provider resolution policy for a council kind.
    /// [JA] council kind に対応する Provider resolution policy を作成します。
    /// </summary>
    /// <param name="councilKind">
    /// [EN] Council kind to resolve.
    /// [JA] 解決対象の council kind です。
    /// </param>
    /// <param name="preferredProviderId">
    /// [EN] Optional preferred provider identifier.
    /// [JA] 任意の preferred Provider 識別子です。
    /// </param>
    /// <returns>
    /// [EN] Provider resolution policy.
    /// [JA] Provider resolution policy です。
    /// </returns>
    public ProviderResolutionPolicy Create(
        CouncilKind councilKind,
        string? preferredProviderId = null)
        => new()
        {
            RequiredCapability = CreateCapabilityName(councilKind),
            PreferredProviderId = preferredProviderId
        };

    /// <summary>
    /// [EN] Creates the deterministic capability name for a council kind.
    /// [JA] council kind に対応する deterministic capability 名を作成します。
    /// </summary>
    /// <param name="councilKind">
    /// [EN] Council kind.
    /// [JA] council kind です。
    /// </param>
    /// <returns>
    /// [EN] Capability name.
    /// [JA] capability 名です。
    /// </returns>
    public static string CreateCapabilityName(CouncilKind councilKind)
        => councilKind switch
        {
            CouncilKind.Logos => "ctg.council.logos",
            CouncilKind.Ethos => "ctg.council.ethos",
            CouncilKind.Pathos => "ctg.council.pathos",
            _ => "ctg.council.unknown"
        };
}
