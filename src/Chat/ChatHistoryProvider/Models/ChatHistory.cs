namespace AIKernel.Providers.ChatHistory.Models;

/// <summary>
/// [EN] Chat history model that preserves message order.
/// [JA] message order を保持する chat history model です。
/// </summary>
public sealed class ChatHistory(IReadOnlyList<ChatMessage> messages)
{
    /// <summary>[EN] Ordered chat messages. [JA] 順序付き chat message です。</summary>
    public IReadOnlyList<ChatMessage> Messages { get; } = messages;
}
