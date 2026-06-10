namespace AIKernel.Providers.ChatOpenAI;

/// <summary>
/// [EN] Python bridge that exposes the public ChatOpenAI provider contract surface.
/// [JA] 公開 ChatOpenAI Provider 契約 surface を公開する Python bridge です。
/// </summary>
public static class ChatOpenAIPythonBridge
{
    /// <summary>
    /// [EN] Creates a deterministic capability descriptor for Python wrappers.
    /// [JA] Python wrapper 用の決定論的 capability descriptor を作成します。
    /// </summary>
    public static object ToContract(
        string providerId,
        string model,
        string endpoint)
    {
        var settings = new ChatOpenAISettings
        {
            ProviderId = providerId,
            Model = model,
            Endpoint = new Uri(endpoint)
        };

        return ChatOpenAICapabilityContracts.ToContract(
            new ChatOpenAICapabilityDescriptor(
                settings.ProviderId,
                settings.Model,
                settings.ToMetadata()));
    }

    /// <summary>
    /// [EN] Creates a provider with default settings.
    /// [JA] default settings の Provider を作成します。
    /// </summary>
    public static object CreateProvider()
        => new ChatOpenAIProvider();

    /// <summary>
    /// [EN] Creates a capability module invoker.
    /// [JA] capability module invoker を作成します。
    /// </summary>
    public static object CreateInvoker()
        => new ChatOpenAIInvoker();
}
