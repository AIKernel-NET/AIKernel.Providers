namespace AIKernel.Providers.MicrosoftAI;

using System.Diagnostics.CodeAnalysis;
using AIKernel.Common.Results;
using AIKernel.Core.Time;
using AIKernel.Dtos.Core;
using Microsoft.Extensions.Logging;

internal sealed class OpenAICompatibleProviderHealthCheck
{
    private static readonly Action<ILogger, string, string, Exception?> LogProviderFailed =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(2101, nameof(LogProviderFailed)),
            "OpenAICompatibleProvider health check failed. ProviderId={ProviderId}, ErrorCode={ErrorCode}");

    private readonly OpenAICompatibleProviderOptions _options;
    private readonly ILogger _logger;
    private readonly IKernelClock _clock;

    /// <summary>
    /// [EN] Provides the OpenAICompatibleProviderHealthCheck public provider contract surface.
    /// [JA] OpenAICompatibleProviderHealthCheck の public provider contract surface を提供します。
    /// </summary>
    public OpenAICompatibleProviderHealthCheck(
        OpenAICompatibleProviderOptions options,
        ILogger logger,
        IKernelClock clock)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// [EN] Provides the GetHealthAsync public provider contract surface.
    /// [JA] GetHealthAsync の public provider contract surface を提供します。
    /// </summary>
    public async Task<ProviderHealthStatus> GetHealthAsync(
        string providerId,
        string name,
        string version,
        bool isInitialized)
    {
        var factory = _options.HealthStatusFactory;
        if (factory is null)
        {
            var exception = new InvalidOperationException(
                "OpenAICompatibleProviderOptions.HealthStatusFactory is not configured.");

            if (_logger.IsEnabled(LogLevel.Error))
            {
                LogProviderFailed(
                    _logger,
                    providerId,
                    "provider_health_status_factory_missing",
                    exception);
            }

            throw new ProviderApiException(
                "Provider health status factory is not configured.",
                exception);
        }

        var context = new OpenAICompatibleProviderHealthContext
        {
            ProviderId = providerId,
            Name = name,
            Version = version,
            ModelId = _options.ModelId,
            IsInitialized = isInitialized,
            CheckedAtUtc = _clock.Now
        };

        var healthStatus = Try
            .Run(() => factory(context))
            .Match(
                error =>
                {
                    if (_logger.IsEnabled(LogLevel.Error))
                    {
                        LogProviderFailed(
                            _logger,
                            providerId,
                            "provider_health_status_creation_failed",
                            new InvalidOperationException(error.Message));
                    }

                    throw new ProviderApiException(
                        "Provider health status could not be created.",
                        new InvalidOperationException(error.Message));
                },
                value => value);

        if (IsNullHealthStatus(healthStatus))
        {
            var exception = new InvalidOperationException(
                "Provider health status factory returned null.");

            if (_logger.IsEnabled(LogLevel.Error))
            {
                LogProviderFailed(
                    _logger,
                    providerId,
                    "provider_health_status_null",
                    exception);
            }

            throw new ProviderApiException(
                "Provider health status was null.",
                exception);
        }

        return await Task.FromResult(healthStatus)
            .ConfigureAwait(false);
    }

    private static bool IsNullHealthStatus(
        [NotNullWhen(false)] ProviderHealthStatus? healthStatus)
    {
        return healthStatus is null;
    }
}
