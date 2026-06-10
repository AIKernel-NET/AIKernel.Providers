namespace AIKernel.Providers.ChatHistory;

/// <summary>
/// [EN] Public capability descriptor for the chat history provider.
/// [JA] ChatHistory Provider の公開 capability descriptor です。
/// </summary>
public sealed record ChatHistoryCapabilityDescriptor(
    string CapabilityId,
    IReadOnlyDictionary<string, string> Metadata);
