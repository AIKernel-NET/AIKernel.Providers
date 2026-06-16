namespace AIKernel.Providers.Council.DependencyInjection;

using AIKernel.Providers.Council.Contracts;
using AIKernel.Providers.Council.Providers;
using AIKernel.Providers.Council.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// [EN] Registers AIKernel council semantic provider services.
/// [JA] AIKernel council semantic Provider service を登録します。
/// </summary>
public static class CouncilProviderServiceCollectionExtensions
{
    /// <summary>
    /// [EN] Adds minimal Logos, Ethos, and Pathos semantic providers.
    /// [JA] 最小 Logos / Ethos / Pathos semantic Provider を追加します。
    /// </summary>
    /// <param name="services">EN:  JA: services パラメーターです。
    /// [EN] Service collection to update.
    /// [JA] 更新対象の service collection です。
    /// </param>
    /// <returns>EN:  JA: 結果を返します。
    /// [EN] The same service collection for fluent composition.
    /// [JA] fluent composition 用に同じ service collection を返します。
    /// </returns>
    public static IServiceCollection AddAIKernelCouncilProviders(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<ICouncilSemanticEvaluationProvider, LogosSemanticEvaluationProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ICouncilSemanticEvaluationProvider, EthosSemanticEvaluationProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ICouncilSemanticEvaluationProvider, PathosSemanticEvaluationProvider>());
        services.TryAddSingleton<CouncilProviderResolutionPolicy>();

        return services;
    }
}
