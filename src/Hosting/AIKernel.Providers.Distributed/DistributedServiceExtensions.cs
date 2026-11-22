using AIKernel.Abstractions.Hosting;
using AIKernel.Abstractions.Providers;
using AIKernel.Abstractions.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AIKernel.Providers.Distributed;

public static class DistributedServiceExtensions
{
    public static IServiceCollection AddDistributedApplicationProvider(this IServiceCollection services, DistributedHostOptions options,
        ISecureCredentialProvider? credentials = null)
    {
        services.AddSingleton(options);
        services.TryAddSingleton(_ => new ApplicationCredentials(options.PrivateDirectory, credentials));
        services.TryAddSingleton<IRequestTokenValidator>(sp => sp.GetRequiredService<ApplicationCredentials>());
        services.TryAddSingleton<IApplicationIdentity>(sp => sp.GetRequiredService<ApplicationCredentials>());
        services.TryAddSingleton<IConnectionDescriptorProvider>(_ => new ConnectionFiles(options.AllowPlainHttp));
        services.TryAddSingleton<IDistributedApplicationHost>(sp =>
        {
            var registrar = sp.GetService<IProviderRegistrar>();
            IEnumerable<IProvider> RegisteredProviders()
            {
                if (registrar == null) yield break;
                foreach (var id in registrar.GetRegisteredIds())
                    if (registrar.TryResolve(id, out var provider) && provider != null) yield return provider;
            }

            return new DistributedApplicationHost(options, sp.GetRequiredService<IApplicationIdentity>(),
                sp.GetRequiredService<IRequestTokenValidator>(), sp.GetRequiredService<IConnectionDescriptorProvider>(),
                sp.GetServices<IApiEndpointProvider>().Concat(RegisteredProviders().OfType<IApiEndpointProvider>()).Distinct(),
                sp.GetServices<IMcpToolProvider>().Concat(RegisteredProviders().OfType<IMcpToolProvider>()).Distinct(),
                sp.GetService<Microsoft.Extensions.Logging.ILogger<DistributedApplicationHost>>());
        });
        return services;
    }

    /// <summary>Register a client-side profile directory, its authenticated transport and heartbeat monitor.</summary>
    public static IServiceCollection AddDistributedApplicationClient(this IServiceCollection services, string privateDirectory,
        bool allowPlainHttp = false, TimeSpan? interval = null, TimeSpan? timeout = null)
    {
        services.TryAddSingleton<IConnectionDescriptorProvider>(_ => new ConnectionFiles(allowPlainHttp));
        services.TryAddSingleton(sp => new ConnectionProfiles(privateDirectory, sp.GetRequiredService<IConnectionDescriptorProvider>()));
        services.TryAddSingleton<IConnectionProfileStore>(sp => sp.GetRequiredService<ConnectionProfiles>());
        services.TryAddSingleton<IConnectionClient>(sp => sp.GetRequiredService<ConnectionProfiles>());
        services.TryAddSingleton<IHeartbeatProvider>(sp => new HeartbeatMonitor(sp.GetRequiredService<IConnectionProfileStore>(),
            sp.GetRequiredService<IConnectionClient>(), interval, timeout));
        services.AddHostedService<ClientMonitor>();
        return services;
    }

    private sealed class ClientMonitor(IHeartbeatProvider monitor) : Microsoft.Extensions.Hosting.BackgroundService
    {
        protected override Task ExecuteAsync(CancellationToken stoppingToken) => monitor.MonitorAsync(stoppingToken);
    }
}
