namespace AIKernel.Providers.CudaCompute;

using AIKernel.Providers.Compute;

/// <summary>
/// [EN] Descriptor for a native module boundary without loading the module.
/// [JA] module を load せずに native module 境界を表す descriptor です。
/// </summary>
public sealed record NativeModuleDescriptor
{
    /// <summary>[EN] Stable module identifier inside the backend package. [JA] backend package 内の安定した module 識別子です。</summary>
    public string ModuleId { get; init; } = string.Empty;

    /// <summary>[EN] Runtime-configurable backend identifier. [JA] runtime-configurable backend 識別子です。</summary>
    public string BackendId { get; init; } = string.Empty;

    /// <summary>[EN] Runtime-configurable module reference URI. [JA] runtime-configurable module reference URI です。</summary>
    public string ModuleRef { get; init; } = string.Empty;

    /// <summary>[EN] Optional ABI version required by the module boundary. [JA] module 境界が要求する任意の ABI version です。</summary>
    public string? AbiVersion { get; init; }

    /// <summary>[EN] Entry point name exported by the module boundary. [JA] module 境界が export する entry point 名です。</summary>
    public string EntryPoint { get; init; } = string.Empty;

    /// <summary>[EN] Entry point descriptors carried without loading the module. [JA] module を load せずに保持する entry point descriptor です。</summary>
    public IReadOnlyList<ComputeEntryPointDescriptor> EntryPoints { get; init; } = [];

    /// <summary>[EN] Optional hash metadata for the referenced artifact. [JA] 参照 artifact 向けの任意の hash metadata です。</summary>
    public HashMetadata Hash { get; init; } = new();

    /// <summary>[EN] Canonical artifact hash expression for the referenced module. [JA] 参照 module 向けの正準 artifact hash expression です。</summary>
    public string? ArtifactHash
    {
        get => string.IsNullOrWhiteSpace(Hash.Expression) ? null : Hash.Expression;
        init => Hash = HashMetadata.FromExpression(value);
    }

    /// <summary>[EN] Additional deterministic metadata. [JA] 追加の deterministic metadata です。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
