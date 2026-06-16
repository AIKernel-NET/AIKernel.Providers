namespace AIKernel.Providers.Audio.DependencyInjection;

using AIKernel.Providers.Audio.Routing;
using AIKernel.Providers.Audio.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// [EN] Registers backend-independent audio provider substrate services.
/// [JA] backend 非依存の audio Provider substrate service を登録します。
/// </summary>
public static class AudioProviderServiceCollectionExtensions
{
    /// <summary>
    /// [EN] Adds audio substrate helpers without registering backend implementations.
    /// [JA] backend implementation を登録せず audio substrate helper を追加します。
    /// </summary>
    /// <param name="services">
    /// [EN] Service collection to update.
    /// [JA] 更新対象の service collection です。
    /// </param>
    /// <returns>
    /// [EN] The same service collection for fluent composition.
    /// [JA] fluent composition 用に同じ service collection を返します。
    /// </returns>
    public static IServiceCollection AddAIKernelAudioProviderSubstrate(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<AudioFormatValidator>();
        services.TryAddSingleton<AudioProviderResolutionPolicy>();

        return services;
    }
}
