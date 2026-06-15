namespace AIKernel.Providers.Perception.DependencyInjection;

using AIKernel.Providers.Perception.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// [EN] Registers backend-independent perception provider substrate services.
/// [JA] backend 非依存の perception Provider substrate service を登録します。
/// </summary>
public static class PerceptionProviderServiceCollectionExtensions
{
    /// <summary>
    /// [EN] Adds perception substrate helpers without registering browser or scenario implementations.
    /// [JA] browser や scenario implementation を登録せず perception substrate helper を追加します。
    /// </summary>
    /// <param name="services">[EN] Service collection to update. [JA] 更新対象の service collection です。</param>
    /// <returns>[EN] The same service collection for fluent composition. [JA] fluent composition 用に同じ service collection を返します。</returns>
    public static IServiceCollection AddAIKernelPerceptionProviderSubstrate(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<PerceptionProviderResolutionPolicy>();

        return services;
    }
}
