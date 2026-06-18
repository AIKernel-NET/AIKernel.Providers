namespace AIKernel.Providers.Compute;

/// <summary>
/// [EN] Descriptor for a compute module entry point.
/// [JA] compute module entry point の descriptor です。
/// </summary>
public sealed record ComputeEntryPointDescriptor
{
    /// <summary>[EN] Stable entry point name. [JA] 安定した entry point 名です。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>[EN] Operation name exposed by the entry point. [JA] entry point が公開する operation 名です。</summary>
    public string Operation { get; init; } = string.Empty;

    /// <summary>[EN] Parameter descriptors accepted by the entry point. [JA] entry point が受け入れる parameter descriptor です。</summary>
    public IReadOnlyList<ComputeParameterDescriptor> Parameters { get; init; } = [];

    /// <summary>[EN] Return descriptor produced by the entry point. [JA] entry point が生成する return descriptor です。</summary>
    public ComputeReturnDescriptor Return { get; init; } = new();

    /// <summary>[EN] Additional deterministic metadata. [JA] 追加の deterministic metadata です。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
