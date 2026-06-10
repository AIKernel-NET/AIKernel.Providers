namespace AIKernel.Providers.ChatOpenAI;

/// <summary>
/// [EN] Settings for the OpenAI-compatible provider boundary.
/// [JA] OpenAI 互換 Provider 境界の設定です。
/// </summary>
public sealed record ChatOpenAISettings
{
    /// <summary>[EN] Provider id. [JA] Provider id です。</summary>
    public string ProviderId { get; init; } = "providers.openai";

    /// <summary>[EN] Human-readable provider name. [JA] 人間可読な Provider 名です。</summary>
    public string Name { get; init; } = "Chat OpenAI Provider";

    /// <summary>[EN] Provider contract version. [JA] Provider 契約 version です。</summary>
    public string Version { get; init; } = "0.1.1";

    /// <summary>[EN] OpenAI-compatible endpoint. [JA] OpenAI 互換 endpoint です。</summary>
    public Uri Endpoint { get; init; } = new("https://api.openai.com/v1");

    /// <summary>[EN] Model used for chat completion. [JA] chat completion で使用する model です。</summary>
    public string Model { get; init; } = "gpt-4.1-mini";

    /// <summary>[EN] Optional embedding model id. [JA] 任意の embedding model id です。</summary>
    public string? EmbeddingModel { get; init; }

    /// <summary>[EN] Optional API key. [JA] 任意の API key です。</summary>
    public string? ApiKey { get; init; }

    /// <summary>[EN] Request timeout. [JA] request timeout です。</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>[EN] Returns deterministic metadata for capability export. [JA] capability export 用の決定論的 metadata を返します。</summary>
    public IReadOnlyDictionary<string, string> ToMetadata()
    {
        var metadata = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["endpoint"] = Endpoint.ToString().TrimEnd('/'),
            ["model"] = Model,
            ["version"] = Version
        };

        if (!string.IsNullOrWhiteSpace(EmbeddingModel))
        {
            metadata["embedding_model"] = EmbeddingModel;
        }

        return metadata;
    }
}
