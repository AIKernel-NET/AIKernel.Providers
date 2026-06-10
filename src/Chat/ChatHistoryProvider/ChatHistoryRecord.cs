namespace AIKernel.Providers.ChatHistory;

/// <summary>
/// [EN] Deterministic chat history record exposed by the ChatHistory provider.
/// [JA] ChatHistory Provider が公開する決定論的 chat history record です。
/// </summary>
public sealed class ChatHistoryRecord
{
    /// <summary>[EN] Message role. [JA] message role です。</summary>
    public required string Role { get; init; }

    /// <summary>[EN] Message content. [JA] message content です。</summary>
    public required string Content { get; init; }

    /// <summary>[EN] Message timestamp. [JA] message timestamp です。</summary>
    public DateTimeOffset Timestamp { get; init; }
}
