namespace AIKernel.Providers.ChatHistory.Models;

/// <summary>
/// [EN] Immutable chat message DTO.
/// [JA] immutable chat message DTO です。
/// </summary>
public record ChatMessage(string Role, string Content, string Timestamp);
