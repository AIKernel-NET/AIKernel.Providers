namespace AIKernel.Providers.Audio.Routing;

using AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Creates substrate routing policies for audio provider capabilities.
/// [JA] audio Provider capability 向けの substrate routing policy を作成します。
/// </summary>
public sealed class AudioProviderResolutionPolicy
{
    /// <summary>
    /// [EN] Creates a playback provider resolution policy.
    /// [JA] playback Provider resolution policy を作成します。
    /// </summary>
    /// <param name="preferredProviderId">
    /// [EN] Optional preferred provider identifier.
    /// [JA] 任意の preferred Provider 識別子です。
    /// </param>
    /// <returns>
    /// [EN] Provider resolution policy.
    /// [JA] Provider resolution policy です。
    /// </returns>
    public ProviderResolutionPolicy CreatePlaybackPolicy(string? preferredProviderId = null)
        => new()
        {
            RequiredCapability = "audio.playback",
            PreferredProviderId = preferredProviderId
        };

    /// <summary>
    /// [EN] Creates a recording provider resolution policy.
    /// [JA] recording Provider resolution policy を作成します。
    /// </summary>
    /// <param name="preferredProviderId">
    /// [EN] Optional preferred provider identifier.
    /// [JA] 任意の preferred Provider 識別子です。
    /// </param>
    /// <returns>
    /// [EN] Provider resolution policy.
    /// [JA] Provider resolution policy です。
    /// </returns>
    public ProviderResolutionPolicy CreateRecordingPolicy(string? preferredProviderId = null)
        => new()
        {
            RequiredCapability = "audio.recording",
            PreferredProviderId = preferredProviderId
        };
}
