namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Validates provider manifest descriptors.
/// [JA] Provider manifest descriptor を検証します。
/// </summary>
public interface IProviderManifestValidator
{
    /// <summary>
    /// [EN] Validates a provider manifest descriptor.
    /// [JA] Provider manifest descriptor を検証します。
    /// </summary>
    /// <param name="descriptor">EN:  JA: descriptor パラメーターです。
    /// [EN] Provider manifest descriptor.
    /// [JA] Provider manifest descriptor です。
    /// </param>
    /// <param name="cancellationToken">EN:  JA: cancellationToken パラメーターです。
    /// [EN] Cancellation token.
    /// [JA] cancellation token です。
    /// </param>
    /// <returns>EN:  JA: 結果を返します。
    /// [EN] Structured validation result.
    /// [JA] 構造化された validation result です。
    /// </returns>
    ValueTask<ProviderManifestValidationResult> ValidateAsync(
        ProviderManifestDescriptor descriptor,
        CancellationToken cancellationToken);
}
