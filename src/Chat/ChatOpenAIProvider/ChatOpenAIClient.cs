using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AIKernel.Abstractions.Providers;

namespace AIKernel.Providers.ChatOpenAI;

/// <summary>
/// [EN] Thin HTTP client for OpenAI-compatible chat completion endpoints.
/// [JA] OpenAI 互換 chat completion endpoint 用の薄い HTTP client です。
/// </summary>
public sealed class ChatOpenAIClient(
    HttpClient httpClient,
    ChatOpenAISettings settings)
{
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly ChatOpenAISettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    /// <summary>
    /// [EN] Sends a chat completion request and returns the primary text.
    /// [JA] chat completion request を送信し primary text を返します。
    /// </summary>
    public async Task<string> CompleteAsync(
        IReadOnlyList<IModelMessage> messages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(_settings.Endpoint, "chat/completions"));

        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        }

        request.Content = JsonContent.Create(new ChatCompletionRequest(
            _settings.Model,
            messages.Select(message => new ChatCompletionMessage(message.Role, message.Content)).ToArray()));

        using var response = await _httpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content
            .ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken)
            .ConfigureAwait(false);

        return payload?.Choices.FirstOrDefault()?.Message.Content ?? string.Empty;
    }

    private sealed record ChatCompletionRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatCompletionMessage> Messages);

    private sealed record ChatCompletionMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatCompletionResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<ChatCompletionChoice> Choices);

    private sealed record ChatCompletionChoice(
        [property: JsonPropertyName("message")] ChatCompletionMessage Message);
}
