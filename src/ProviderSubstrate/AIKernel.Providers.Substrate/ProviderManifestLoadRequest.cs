namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Runtime-configurable request for loading a provider manifest.
/// [JA] Provider manifest を load するための runtime-configurable request です。
/// </summary>
public sealed record ProviderManifestLoadRequest
{
    /// <summary>[EN] Manifest JSON text. [JA] manifest JSON text です。</summary>
    public string? Json { get; init; }

    /// <summary>[EN] Manifest file path. [JA] manifest file path です。</summary>
    public string? Path { get; init; }

    /// <summary>[EN] Manifest stream. [JA] manifest stream です。</summary>
    public Stream? Stream { get; init; }

    /// <summary>[EN] Optional source label. [JA] 任意の source label です。</summary>
    public string? Source { get; init; }

    /// <summary>[EN] Additional deterministic metadata. [JA] 追加の deterministic metadata です。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
