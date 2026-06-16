namespace AIKernel.Providers.CudaCompute.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// [EN] Registers descriptor-driven CUDA compute provider boundary services.
/// [JA] descriptor-driven CUDA compute Provider 境界 service を登録します。
/// </summary>
public static class CudaComputeProviderServiceCollectionExtensions
{
    /// <summary>
    /// [EN] Adds CUDA compute descriptor and invoker boundary services without binding a native CUDA runtime.
    /// [JA] native CUDA runtime を bind せず CUDA compute descriptor と invoker 境界 service を追加します。
    /// </summary>
    /// <param name="services">EN:  JA: services パラメーターです。
    /// [EN] Service collection to update.
    /// [JA] 更新対象の service collection です。
    /// </param>
    /// <returns>EN:  JA: 結果を返します。
    /// [EN] The same service collection for fluent composition.
    /// [JA] fluent composition 用に同じ service collection を返します。
    /// </returns>
    public static IServiceCollection AddCudaComputeProvider(this IServiceCollection services)
        => AddCudaComputeProvider(services, new CudaComputeSettings());

    /// <summary>
    /// [EN] Adds CUDA compute descriptor and invoker boundary services with explicit settings.
    /// [JA] 明示的な settings を使用して CUDA compute descriptor と invoker 境界 service を追加します。
    /// </summary>
    /// <param name="services">EN:  JA: services パラメーターです。
    /// [EN] Service collection to update.
    /// [JA] 更新対象の service collection です。
    /// </param>
    /// <param name="settings">EN:  JA: settings パラメーターです。
    /// [EN] Runtime-configurable CUDA compute provider settings.
    /// [JA] runtime-configurable な CUDA compute Provider settings です。
    /// </param>
    /// <returns>EN:  JA: 結果を返します。
    /// [EN] The same service collection for fluent composition.
    /// [JA] fluent composition 用に同じ service collection を返します。
    /// </returns>
    public static IServiceCollection AddCudaComputeProvider(
        this IServiceCollection services,
        CudaComputeSettings settings)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(settings);

        services.TryAddSingleton(settings);
        services.TryAddSingleton<CudaComputeProvider>();
        services.TryAddSingleton<CudaComputeInvoker>();
        services.TryAddSingleton<CudaBackendResolver>();
        services.TryAddSingleton<CudaBackendResolutionPolicy>();

        return services;
    }
}
