namespace AIKernel.Providers.ChatHistory;

/// <summary>
/// [EN] Python bridge that exposes the public chat history provider contract surface.
/// [JA] 公開 ChatHistory Provider 契約 surface を公開する Python bridge です。
/// </summary>
public static class ChatHistoryPythonBridge
{
    /// <summary>
    /// [EN] Creates a deterministic capability descriptor for Python wrappers.
    /// [JA] Python wrapper 用の決定論的 capability descriptor を作成します。
    /// </summary>
    public static object ToContract(
        string providerId)
    {
        var settings = new ChatHistorySettings
        {
            ProviderId = providerId
        };

        return ChatHistoryCapabilityContracts.ToContract(
            new ChatHistoryCapabilityDescriptor(
                settings.ProviderId,
                settings.ToMetadata()));
    }

    /// <summary>
    /// [EN] Creates a provider with default settings.
    /// [JA] default settings の Provider を作成します。
    /// </summary>
    public static object CreateProvider()
        => new ChatHistoryProvider();

    /// <summary>
    /// [EN] Creates a capability module invoker.
    /// [JA] capability module invoker を作成します。
    /// </summary>
    public static object CreateInvoker()
        => new ChatHistoryInvoker();
}
