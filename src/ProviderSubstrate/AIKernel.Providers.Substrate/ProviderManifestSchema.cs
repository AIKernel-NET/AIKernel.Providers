namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Provider manifest schema metadata used by tooling and validation.
/// [JA] tooling と validation が利用する Provider manifest schema metadata です。
/// </summary>
public sealed record ProviderManifestSchema
{
    /// <summary>[EN] Manifest schema version. [JA] manifest schema version です。</summary>
    public string Version { get; init; } = "1.0";

    /// <summary>[EN] Optional schema URI. [JA] 任意の schema URI です。</summary>
    public string? SchemaUri { get; init; }

    /// <summary>[EN] Required top-level manifest fields. [JA] 必須の top-level manifest field です。</summary>
    public IReadOnlyList<string> RequiredFields { get; init; } =
        ["providerId", "name", "version", "assembly"];

    /// <summary>[EN] Optional extension fields known to the current schema. [JA] 現 schema が知る任意の extension field です。</summary>
    public IReadOnlyList<string> OptionalFields { get; init; } =
        ["capabilities", "priority", "metadata", "backendMetadata", "backendDescriptors", "vendorMetadata", "cli", "tags"];
}
