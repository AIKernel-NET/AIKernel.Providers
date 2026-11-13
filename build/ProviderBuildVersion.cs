using System.Reflection;

namespace AIKernel.Providers;

// Compiled into each provider so package overrides also reach runtime discovery.
internal static class ProviderBuildVersion
{
    internal static string Value { get; } =
        typeof(ProviderBuildVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0]
        ?? typeof(ProviderBuildVersion).Assembly.GetName().Version!.ToString(3);
}
