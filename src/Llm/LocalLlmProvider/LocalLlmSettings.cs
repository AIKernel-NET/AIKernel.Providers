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
    public string Version { get; init; } = global::AIKernel.Providers.ProviderBuildVersion.Value;

    /// <summary>[EN] Local runtime name. [JA] local runtime 名です。</summary>
    public string Runtime { get; init; } = "aikernel-bonsai";

    /// <summary>[EN] Optional local runtime URI. [JA] 任意の local runtime URI です。</summary>
    public string? RuntimeUri { get; init; }

    /// <summary>[EN] Optional runtime artifact hash. [JA] 任意の runtime artifact hash です。</summary>
    public string? ArtifactHash { get; init; }

    /// <summary>Host-configured path to the local Bonsai 1.7B Q1_0 GGUF file.</summary>
    public string? BonsaiModelPath { get; init; }

    /// <summary>CPU worker count, from 1 to 6.</summary>
    public int CpuThreads { get; init; } = 4;

    /// <summary>Total token budget, from 64 to 2048, including the chat template and reply.</summary>
    public int ContextTokens { get; init; } = 1024;

    /// <summary>Maximum prompt length in UTF-16 code units, from 1 to 4096.</summary>
    public int MaxPromptCharacters { get; init; } = 4096;

    /// <summary>Default and maximum number of reply tokens, from 1 to 256 and below the context budget.</summary>
    public int MaxNewTokens { get; init; } = 128;

    /// <summary>Trusted system prompt supplied by the host. The caller supplies only the user prompt.</summary>
    public string SystemPrompt { get; init; } = "You are a helpful assistant. Answer in the user's language.";

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
