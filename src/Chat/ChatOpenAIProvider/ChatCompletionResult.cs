namespace AIKernel.Providers.ChatOpenAI;

/// <summary>[EN] Completion text, finish reason, and reported upstream usage.
/// [JA] 回答、終了理由、および上流が報告したトークン使用量です。</summary>
/// <param name="Content">[EN] Response text. [JA] 回答本文。</param>
/// <param name="FinishReason">[EN] Upstream finish reason, if present. [JA] 上流の終了理由。</param>
/// <param name="PromptTokens">[EN] Reported input tokens; null means unknown. [JA] 入力トークン数。nullは不明。</param>
/// <param name="CompletionTokens">[EN] Reported output tokens; null means unknown. [JA] 出力トークン数。nullは不明。</param>
public sealed record ChatCompletionResult(string Content, string? FinishReason, int? PromptTokens, int? CompletionTokens);
