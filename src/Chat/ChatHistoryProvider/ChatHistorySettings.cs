namespace AIKernel.Providers.ChatHistory;

/// <summary>
/// [EN] Settings for the chat history provider boundary.
/// [JA] ChatHistory Provider 境界の設定です。
/// </summary>
public sealed record ChatHistorySettings
{
    /// <summary>[EN] Provider id. [JA] Provider id です。</summary>
    public string ProviderId { get; init; } = "chat-history";

    /// <summary>[EN] Human-readable provider name. [JA] 人間可読な Provider 名です。</summary>
    public string Name { get; init; } = "Chat History Provider";

    /// <summary>[EN] Provider contract version. [JA] Provider 契約 version です。</summary>
    public string Version { get; init; } = "0.1.1";

    /// <summary>[EN] Optional history source URI. [JA] 任意の history source URI です。</summary>
    public string? SourceUri { get; init; } = "rom://providers/chat-history/history.json";

    /// <summary>[EN] Optional source artifact hash. [JA] 任意の source artifact hash です。</summary>
    public string? ArtifactHash { get; init; }

    /// <summary>[EN] Returns deterministic metadata for capability export. [JA] capability export 用の決定論的 metadata を返します。</summary>
    public IReadOnlyDictionary<string, string> ToMetadata()
    {
        var metadata = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["version"] = Version
        };

        if (!string.IsNullOrWhiteSpace(SourceUri))
        {
            metadata["source_uri"] = SourceUri;
        }

        if (!string.IsNullOrWhiteSpace(ArtifactHash))
        {
            metadata["artifact_hash"] = ArtifactHash;
        }

        return metadata;
    }
}
