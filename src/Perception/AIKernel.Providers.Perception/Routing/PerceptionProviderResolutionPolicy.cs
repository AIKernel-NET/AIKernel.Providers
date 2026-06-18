namespace AIKernel.Providers.Perception.Routing;

using AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Creates deterministic substrate routing policies for perception provider capabilities.
/// [JA] perception Provider capability 向け deterministic substrate routing policy を作成します。
/// </summary>
public sealed class PerceptionProviderResolutionPolicy
{
    /// <summary>
    /// [EN] Creates a frame perception provider resolution policy.
    /// [JA] frame perception Provider resolution policy を作成します。
    /// </summary>
    /// <param name="preferredProviderId">[EN] Optional preferred provider identifier. [JA] 任意の preferred Provider 識別子です。</param>
    /// <returns>[EN] Provider resolution policy. [JA] Provider resolution policy を返します。</returns>
    public ProviderResolutionPolicy CreateFramePolicy(string? preferredProviderId = null)
        => new()
        {
            RequiredCapability = "perception.frame",
            PreferredProviderId = preferredProviderId
        };

    /// <summary>
    /// [EN] Creates an auditory perception provider resolution policy.
    /// [JA] auditory perception Provider resolution policy を作成します。
    /// </summary>
    /// <param name="preferredProviderId">[EN] Optional preferred provider identifier. [JA] 任意の preferred Provider 識別子です。</param>
    /// <returns>[EN] Provider resolution policy. [JA] Provider resolution policy を返します。</returns>
    public ProviderResolutionPolicy CreateAuditoryPolicy(string? preferredProviderId = null)
        => new()
        {
            RequiredCapability = "perception.auditory",
            PreferredProviderId = preferredProviderId
        };

    /// <summary>
    /// [EN] Creates a spatial cognition provider resolution policy.
    /// [JA] spatial cognition Provider resolution policy を作成します。
    /// </summary>
    /// <param name="preferredProviderId">[EN] Optional preferred provider identifier. [JA] 任意の preferred Provider 識別子です。</param>
    /// <returns>[EN] Provider resolution policy. [JA] Provider resolution policy を返します。</returns>
    public ProviderResolutionPolicy CreateSpatialPolicy(string? preferredProviderId = null)
        => new()
        {
            RequiredCapability = "perception.spatial",
            PreferredProviderId = preferredProviderId
        };
}
