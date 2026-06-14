namespace AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Descriptor for an optional provider dependency declared by a manifest.
/// [JA] manifest が宣言する任意の Provider dependency descriptor です。
/// </summary>
public sealed record ProviderDependencyDescriptor
{
    /// <summary>[EN] Dependency identifier. [JA] dependency 識別子です。</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>[EN] Dependency kind such as package, endpoint, or backend. [JA] package、endpoint、backend などの dependency kind です。</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>[EN] Optional version or range expression. [JA] 任意の version または range expression です。</summary>
    public string? Version { get; init; }

    /// <summary>[EN] Indicates whether the dependency is optional. [JA] dependency が optional かどうかを示します。</summary>
    public bool Optional { get; init; } = true;

    /// <summary>[EN] Additional deterministic metadata. [JA] 追加の deterministic metadata です。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
