namespace AIKernel.Providers.LocalLlm;

/// <summary>
/// [EN] Settings for the local LLM provider boundary.
/// [JA] Local LLM Provider 境界の設定です。
/// </summary>
public sealed record LocalLlmSettings
{
    /// <summary>[EN] Provider id. [JA] Provider id です。</summary>
    public string ProviderId { get; init; } = "providers.local-llm";

    /// <summary>[EN] Human-readable provider name. [JA] 人間可読な Provider 名です。</summary>
    public string Name { get; init; } = "Local LLM Provider";

    /// <summary>[EN] Provider contract version. [JA] Provider 契約 version です。</summary>
    public string Version { get; init; } = "0.1.1";

    /// <summary>[EN] Local runtime name. [JA] local runtime 名です。</summary>
    public string Runtime { get; init; } = "ollama";

    /// <summary>[EN] Optional local runtime URI. [JA] 任意の local runtime URI です。</summary>
    public string? RuntimeUri { get; init; } = "ollama://localhost";

    /// <summary>[EN] Optional runtime artifact hash. [JA] 任意の runtime artifact hash です。</summary>
    public string? ArtifactHash { get; init; }

    /// <summary>[EN] Returns deterministic metadata for capability export. [JA] capability export 用の決定論的 metadata を返します。</summary>
    public IReadOnlyDictionary<string, string> ToMetadata()
    {
        var metadata = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["runtime"] = Runtime,
            ["version"] = Version
        };

        if (!string.IsNullOrWhiteSpace(RuntimeUri))
        {
            metadata["runtime_uri"] = RuntimeUri;
        }

        if (!string.IsNullOrWhiteSpace(ArtifactHash))
        {
            metadata["artifact_hash"] = ArtifactHash;
        }

        return metadata;
    }
}
