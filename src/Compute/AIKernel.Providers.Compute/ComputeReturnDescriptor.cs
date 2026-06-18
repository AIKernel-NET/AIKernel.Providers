namespace AIKernel.Providers.Compute;

/// <summary>
/// [EN] Descriptor for a compute entry point return value.
/// [JA] compute entry point return value の descriptor です。
/// </summary>
public sealed record ComputeReturnDescriptor
{
    /// <summary>[EN] Standard dtype name for tensor-like returns. [JA] tensor-like return 向けの標準 dtype 名です。</summary>
    public string DType { get; init; } = ComputeBufferDTypes.Unknown;

    /// <summary>[EN] Optional shape expression such as 1,256,256. [JA] 1,256,256 などの任意の shape expression です。</summary>
    public string? Shape { get; init; }

    /// <summary>[EN] Additional deterministic metadata. [JA] 追加の deterministic metadata です。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
