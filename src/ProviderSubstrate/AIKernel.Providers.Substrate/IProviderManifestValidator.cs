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
    /// <param name="descriptor">
    /// [EN] Provider manifest descriptor.
    /// [JA] Provider manifest descriptor です。
    /// </param>
    /// <param name="cancellationToken">
    /// [EN] Cancellation token.
    /// [JA] cancellation token です。
    /// </param>
    /// <returns>
    /// [EN] Structured validation result.
    /// [JA] 構造化された validation result です。
    /// </returns>
    ValueTask<ProviderManifestValidationResult> ValidateAsync(
        ProviderManifestDescriptor descriptor,
        CancellationToken cancellationToken);
}
