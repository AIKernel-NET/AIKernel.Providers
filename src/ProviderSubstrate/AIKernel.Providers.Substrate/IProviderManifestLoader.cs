namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Loads provider manifests into canonical descriptors.
/// [JA] Provider manifest を canonical descriptor へ load します。
/// </summary>
public interface IProviderManifestLoader
{
    /// <summary>
    /// [EN] Loads a provider manifest from a runtime-configurable request.
    /// [JA] runtime-configurable request から Provider manifest を load します。
    /// </summary>
    /// <param name="request">EN:  JA: request パラメーターです。
    /// [EN] Manifest load request.
    /// [JA] manifest load request です。
    /// </param>
    /// <param name="cancellationToken">EN:  JA: cancellationToken パラメーターです。
    /// [EN] Cancellation token.
    /// [JA] cancellation token です。
    /// </param>
    /// <returns>EN:  JA: 結果を返します。
    /// [EN] Structured manifest load result.
    /// [JA] 構造化された manifest load result です。
    /// </returns>
    ValueTask<ProviderManifestLoadResult> LoadAsync(
        ProviderManifestLoadRequest request,
        CancellationToken cancellationToken);
}
