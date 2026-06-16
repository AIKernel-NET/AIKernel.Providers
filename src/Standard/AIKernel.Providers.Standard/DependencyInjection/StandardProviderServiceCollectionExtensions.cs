namespace AIKernel.Providers.Standard.DependencyInjection;

using AIKernel.Abstractions.Compute;
using AIKernel.Abstractions.Logging;
using AIKernel.Abstractions.Network;
using AIKernel.Abstractions.Processes;
using AIKernel.Abstractions.Providers;
using AIKernel.Providers.Standard.Compute;
using AIKernel.Providers.Standard.FileSystem;
using AIKernel.Providers.Standard.Logging;
using AIKernel.Providers.Standard.Network;
using AIKernel.Providers.Standard.Processes;
using AIKernel.Vfs;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// [EN] Registers the standard AIKernel OS driver providers.
/// [JA] 標準 AIKernel OS driver Provider 群を登録します。
/// </summary>
public static class StandardProviderServiceCollectionExtensions
{
    /// <summary>
    /// [EN] Adds CPU compute, in-memory file system, console logging, process supervision, and HTTP networking providers.
    /// [JA] CPU compute、memory file system、console logging、process supervision、HTTP networking Provider を追加します。
    /// </summary>
    /// <param name="services">EN:  JA: services パラメーターです。
    /// [EN] Service collection to update.
    /// [JA] 更新対象の service collection です。
    /// </param>
    /// <returns>EN:  JA: 結果を返します。
    /// [EN] The same service collection for fluent composition.
    /// [JA] fluent composition 用に同じ service collection を返します。
    /// </returns>
    public static IServiceCollection AddAIKernelStandardProviders(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<CpuComputeProvider>();
        services.AddSingleton<IComputeProvider>(static provider => provider.GetRequiredService<CpuComputeProvider>());
        services.AddSingleton<IProvider>(static provider => provider.GetRequiredService<CpuComputeProvider>());

        services.AddSingleton<MemoryFileSystemProvider>();
        services.AddSingleton<IFileSystemProvider>(static provider => provider.GetRequiredService<MemoryFileSystemProvider>());
        services.AddSingleton<IProvider>(static provider => provider.GetRequiredService<MemoryFileSystemProvider>());

        services.AddSingleton<ConsoleLoggingProvider>();
        services.AddSingleton<ILoggingProvider>(static provider => provider.GetRequiredService<ConsoleLoggingProvider>());
        services.AddSingleton<IProvider>(static provider => provider.GetRequiredService<ConsoleLoggingProvider>());

        services.AddSingleton<DefaultProcessSupervisorProvider>();
        services.AddSingleton<IProcessSupervisorProvider>(static provider => provider.GetRequiredService<DefaultProcessSupervisorProvider>());
        services.AddSingleton<IProvider>(static provider => provider.GetRequiredService<DefaultProcessSupervisorProvider>());

        services.AddSingleton<HttpNetworkProvider>();
        services.AddSingleton<INetworkProvider>(static provider => provider.GetRequiredService<HttpNetworkProvider>());
        services.AddSingleton<IProvider>(static provider => provider.GetRequiredService<HttpNetworkProvider>());

        return services;
    }
}
