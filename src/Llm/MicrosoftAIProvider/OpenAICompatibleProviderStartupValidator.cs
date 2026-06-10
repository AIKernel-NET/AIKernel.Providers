namespace AIKernel.Providers.MicrosoftAI;

using AIKernel.Abstractions.Security;
using AIKernel.Core.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

internal sealed class OpenAICompatibleProviderStartupValidator(
    IOptions<OpenAICompatibleProviderOptions> options,
    ISecureCredentialProvider credentialProvider) : IHostedService
{
    private readonly IOptions<OpenAICompatibleProviderOptions> _options =
        options ?? throw new ArgumentNullException(nameof(options));

    private readonly ISecureCredentialProvider _credentialProvider =
        credentialProvider ?? throw new ArgumentNullException(nameof(credentialProvider));

    /// <summary>
    /// [EN] Provides the StartAsync public provider contract surface.
    /// [JA] StartAsync の public provider contract surface を提供します。
    /// </summary>
    public async Task StartAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var value = _options.Value;

        var hasApiKey = !string.IsNullOrWhiteSpace(value.ApiKey);
        var hasSecretKeyName = !string.IsNullOrWhiteSpace(value.SecretKeyName);

        if (!hasApiKey && !hasSecretKeyName)
        {
            throw new SecureCredentialNotFoundException(
                $"{nameof(OpenAICompatibleProviderOptions)}.ApiKey");
        }

        if (hasApiKey && hasSecretKeyName)
        {
            throw new SecureCredentialAmbiguousException(
                "Do not specify both ApiKey and SecretKeyName in OpenAICompatibleProviderOptions.");
        }

        if (hasSecretKeyName)
        {
            var secretKeyName = value.SecretKeyName!;
            var secret = await _credentialProvider
                .GetSecretAsync(secretKeyName, cancellationToken)
                .ConfigureAwait(false);

            SecureCredentialGuard.ValidateSecret(
                secretKeyName,
                secret);

            value.ApiKey = secret;

            return;
        }

        SecureCredentialGuard.ValidateSecret(
            $"{nameof(OpenAICompatibleProviderOptions)}.ApiKey",
            value.ApiKey);
    }

    /// <summary>
    /// [EN] Provides the StopAsync public provider contract surface.
    /// [JA] StopAsync の public provider contract surface を提供します。
    /// </summary>
    public Task StopAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.CompletedTask;
    }
}
