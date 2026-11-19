using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIKernel.Abstractions.Providers;

namespace AIKernel.Providers.ChatOpenAI;

/// <summary>
/// [EN] HTTP client for OpenAI-compatible chat completion and embedding endpoints.
/// [JA] OpenAI互換のチャット補完と埋め込み用のHTTPクライアントです。
/// </summary>
public sealed partial class ChatOpenAIClient(
    HttpClient httpClient,
    ChatOpenAISettings settings)
{
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly ChatOpenAISettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    /// <summary>
    /// [EN] Sends a chat completion request and returns the primary text.
    /// [JA] チャット補完を要求し、最初の回答を返します。
    /// </summary>
    public async Task<string> CompleteAsync(
        IReadOnlyList<IModelMessage> messages,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateChatRequest(messages, stream: false);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content
            .ReadFromJsonAsync(ChatJsonContext.Default.ChatCompletionResponse, cancellationToken)
            .ConfigureAwait(false);

        return payload?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
    }

    /// <summary>
    /// [EN] Delivers text deltas as they arrive and returns the upstream finish reason.
    /// [JA] 受信したテキスト差分を逐次渡し、上流の終了理由を返します。
    /// </summary>
    public async Task<string> StreamCompleteAsync(
        IReadOnlyList<IModelMessage> messages,
        Func<string, Task> onChunk,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onChunk);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_settings.Timeout);
        var token = timeout.Token;
        using var request = CreateChatRequest(messages, stream: true);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (!string.Equals(response.Content.Headers.ContentType?.MediaType, "text/event-stream", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The chat provider did not return an event stream.");
        }

        using var body = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        string? finishReason = null;
        await foreach (var item in SseParser.Create(body).EnumerateAsync(token).ConfigureAwait(false))
        {
            if (item.Data == "[DONE]")
            {
                token.ThrowIfCancellationRequested();
                return finishReason ?? throw new InvalidDataException("The chat provider did not finish a choice.");
            }

            var chunk = JsonSerializer.Deserialize(item.Data, ChatJsonContext.Default.ChatStreamChunk);
            if (chunk?.Choices is null)
            {
                throw new InvalidDataException("The chat provider returned an invalid stream frame.");
            }

            foreach (var choice in chunk.Choices)
            {
                if (choice?.Index != 0)
                {
                    continue;
                }

                if (finishReason is not null)
                {
                    throw new InvalidDataException("The chat provider continued a finished choice.");
                }

                if (choice.Delta?.Content is { Length: > 0 } text)
                {
                    token.ThrowIfCancellationRequested();
                    await onChunk(text).ConfigureAwait(false);
                }

                if (!string.IsNullOrWhiteSpace(choice.FinishReason))
                {
                    finishReason = choice.FinishReason;
                }
            }
        }

        throw new InvalidDataException("The chat provider disconnected before the stream finished.");
    }

    /// <summary>
    /// [EN] Embeds a batch of texts and returns the upstream vectors in input order.
    /// [JA] 複数のテキストを埋め込み、上流から受け取ったベクトルを入力順に返します。
    /// </summary>
    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texts);
        var inputs = texts.ToArray();
        if (inputs.Length is 0 or > 2048 || inputs.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Provide one to 2048 non-empty texts.", nameof(texts));
        }

        var model = _settings.EmbeddingModel;
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException("An embedding model must be configured.");
        }

        using var request = CreateRequest("embeddings");
        request.Content = JsonContent.Create(
            new EmbeddingRequest(model, inputs, "float"), ChatJsonContext.Default.EmbeddingRequest);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content
            .ReadFromJsonAsync(ChatJsonContext.Default.EmbeddingResponse, cancellationToken).ConfigureAwait(false);
        if (payload?.Data is not { } data || data.Length != inputs.Length)
        {
            throw new InvalidDataException("The embedding provider returned the wrong number of vectors.");
        }

        var vectors = new float[inputs.Length][];
        int? dimensions = null;
        foreach (var item in data)
        {
            if (item?.Index is not int index || index < 0 || index >= vectors.Length || vectors[index] is not null ||
                item.Vector is not { Length: > 0 } vector || vector.Any(value => !float.IsFinite(value)) ||
                (dimensions is int expected && vector.Length != expected))
            {
                throw new InvalidDataException("The embedding provider returned an invalid vector or index.");
            }

            vectors[index] = vector;
            dimensions = vector.Length;
        }

        return vectors;
    }

    internal static string SerializeEmbedding(float[] vector) =>
        JsonSerializer.Serialize(vector, ChatJsonContext.Default.SingleArray);

    private HttpRequestMessage CreateChatRequest(IReadOnlyList<IModelMessage> messages, bool stream)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var payload = new ChatCompletionRequest(_settings.Model,
            messages.Select(message => new ChatCompletionMessage(message.Role, message.Content)).ToArray(), stream);
        var request = CreateRequest("chat/completions");
        request.Content = JsonContent.Create(payload, ChatJsonContext.Default.ChatCompletionRequest);
        return request;
    }

    private HttpRequestMessage CreateRequest(string operation)
    {
        var endpoint = new UriBuilder(_settings.Endpoint)
        {
            Path = _settings.Endpoint.AbsolutePath.TrimEnd('/') + "/" + operation
        };
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Uri);
        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        }

        return request;
    }

    private sealed record ChatCompletionRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatCompletionMessage> Messages,
        [property: JsonPropertyName("stream"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Stream);

    private sealed record ChatCompletionMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatCompletionResponse(
        [property: JsonPropertyName("choices")] ChatCompletionChoice[]? Choices);

    private sealed record ChatCompletionChoice(
        [property: JsonPropertyName("message")] ChatCompletionMessage? Message);

    private sealed record ChatStreamChunk(
        [property: JsonPropertyName("choices")] ChatStreamChoice?[]? Choices);

    private sealed record ChatStreamChoice(
        [property: JsonPropertyName("index")] int? Index,
        [property: JsonPropertyName("delta")] ChatStreamDelta? Delta,
        [property: JsonPropertyName("finish_reason")] string? FinishReason);

    private sealed record ChatStreamDelta([property: JsonPropertyName("content")] string? Content);

    private sealed record EmbeddingRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] string[] Input,
        [property: JsonPropertyName("encoding_format")] string EncodingFormat);

    private sealed record EmbeddingResponse(
        [property: JsonPropertyName("data")] EmbeddingData?[]? Data);

    private sealed record EmbeddingData(
        [property: JsonPropertyName("index")] int? Index,
        [property: JsonPropertyName("embedding")] float[]? Vector);

    [JsonSerializable(typeof(ChatCompletionRequest))]
    [JsonSerializable(typeof(ChatCompletionResponse))]
    [JsonSerializable(typeof(ChatStreamChunk))]
    [JsonSerializable(typeof(EmbeddingRequest))]
    [JsonSerializable(typeof(EmbeddingResponse))]
    [JsonSerializable(typeof(float[]))]
    private sealed partial class ChatJsonContext : JsonSerializerContext;
}
