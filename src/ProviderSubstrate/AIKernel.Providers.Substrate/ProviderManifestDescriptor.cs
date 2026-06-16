namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Runtime-configurable provider manifest descriptor.
/// [JA] runtime-configurable な Provider manifest descriptor です。
/// </summary>
public sealed record ProviderManifestDescriptor
{
    /// <summary>[EN] Manifest schema version. [JA] manifest schema version です。</summary>
    public string SchemaVersion { get; init; } = "1.0";

    /// <summary>[EN] Manifest compatibility version. [JA] manifest compatibility version です。</summary>
    public string ManifestVersion { get; init; } = "1.0";

    /// <summary>[EN] Stable provider identifier. [JA] 安定した Provider 識別子です。</summary>
    public string ProviderId { get; init; } = string.Empty;

    /// <summary>[EN] Human-readable provider name. [JA] 人間可読な Provider 名です。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>[EN] Provider version declared by the manifest. [JA] manifest が宣言する Provider version です。</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>[EN] Managed assembly file or package-local assembly name. [JA] managed assembly file または package-local assembly 名です。</summary>
    public string AssemblyName { get; init; } = string.Empty;

    /// <summary>[EN] Optional package identifier. [JA] 任意の package 識別子です。</summary>
    public string? PackageId { get; init; }

    /// <summary>[EN] Optional provider implementation type name. [JA] 任意の Provider implementation type 名です。</summary>
    public string? ProviderType { get; init; }

    /// <summary>[EN] Optional invoker implementation type name. [JA] 任意の invoker implementation type 名です。</summary>
    public string? InvokerType { get; init; }

    /// <summary>[EN] Capability names exported by the provider. [JA] Provider が export する capability 名です。</summary>
    public IReadOnlyList<string> Capabilities { get; init; } = [];

    /// <summary>[EN] Provider priority; lower values are preferred. [JA] Provider priority です。小さい値を優先します。</summary>
    public int Priority { get; init; }

    /// <summary>[EN] Deterministic routing tags. [JA] deterministic routing tag です。</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>[EN] Manifest metadata projected to string values. [JA] string 値へ射影された manifest metadata です。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>[EN] Backend-level metadata that overrides provider-level metadata for backend resolution. [JA] backend resolution では provider-level metadata を override する backend-level metadata です。</summary>
    public IReadOnlyDictionary<string, string> BackendMetadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>[EN] Backend descriptors used for deterministic backend preference. [JA] deterministic backend preference で利用する backend descriptor です。</summary>
    public IReadOnlyList<ProviderBackendDescriptor> BackendDescriptors { get; init; } = [];

    /// <summary>[EN] Optional dependency descriptors declared by the manifest. [JA] manifest が宣言する任意の dependency descriptor です。</summary>
    public IReadOnlyList<ProviderDependencyDescriptor> Dependencies { get; init; } = [];

    /// <summary>[EN] Optional compatibility descriptor declared by the manifest. [JA] manifest が宣言する任意の compatibility descriptor です。</summary>
    public ProviderCompatibilityDescriptor Compatibility { get; init; } = new();

    /// <summary>[EN] Vendor-level metadata that overrides provider-level and backend-level metadata only within the manifest boundary. [JA] manifest 境界内で provider-level / backend-level metadata を override する vendor-level metadata です。</summary>
    public IReadOnlyDictionary<string, string> VendorMetadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>[EN] CLI hints inherited by provider tooling. [JA] Provider tooling が継承する CLI hint です。</summary>
    public ProviderCliHints CliHints { get; init; } = new();

    /// <summary>[EN] Unknown top-level JSON members preserved as raw JSON text. [JA] raw JSON text として保持する未知の top-level JSON member です。</summary>
    public IReadOnlyDictionary<string, string> ExtensionJson { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>[EN] Optional manifest source path or URI. [JA] 任意の manifest source path または URI です。</summary>
    public string? Source { get; init; }

    /// <summary>
    /// [EN] Creates a normalized capability descriptor for deterministic routing.
    /// [JA] deterministic routing 用の正規化された capability descriptor を作成します。
    /// </summary>
    /// <returns>EN:  JA: 結果を返します。
    /// [EN] Normalized capability descriptor.
    /// [JA] 正規化された capability descriptor です。
    /// </returns>
    public ProviderCapabilityDescriptor ToCapabilityDescriptor()
        => new()
        {
            ProviderId = ProviderId,
            Capabilities = Capabilities,
            Priority = Priority,
            Tags = Tags,
            Metadata = new ProviderManifestMergePolicy().MergeMetadata(this)
        };
}
