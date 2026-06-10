namespace AIKernel.Providers.LocalLlm;

/// <summary>
/// [EN] Python bridge that exposes the public local LLM provider contract surface.
/// [JA] 公開 Local LLM Provider 契約 surface を公開する Python bridge です。
/// </summary>
public static class LocalLlmPythonBridge
{
    /// <summary>
    /// [EN] Creates a deterministic capability descriptor for Python wrappers.
    /// [JA] Python wrapper 用の決定論的 capability descriptor を作成します。
    /// </summary>
    public static object ToContract(
        string providerId,
        string runtime)
    {
        var settings = new LocalLlmSettings
        {
            ProviderId = providerId,
            Runtime = runtime
        };

        return LocalLlmCapabilityContracts.ToContract(
            new LocalLlmCapabilityDescriptor(
                settings.ProviderId,
                settings.Runtime,
                settings.ToMetadata()));
    }

    /// <summary>
    /// [EN] Creates a provider with default settings.
    /// [JA] default settings の Provider を作成します。
    /// </summary>
    public static object CreateProvider()
        => new LocalLlmProvider();

    /// <summary>
    /// [EN] Creates a capability module invoker.
    /// [JA] capability module invoker を作成します。
    /// </summary>
    public static object CreateInvoker()
        => new LocalLlmInvoker();
}
