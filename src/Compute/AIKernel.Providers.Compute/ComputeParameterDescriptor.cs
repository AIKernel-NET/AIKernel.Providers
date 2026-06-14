namespace AIKernel.Providers.Compute;

/// <summary>
/// [EN] Descriptor for a compute entry point parameter.
/// [JA] compute entry point parameter の descriptor です。
/// </summary>
public sealed record ComputeParameterDescriptor
{
    /// <summary>[EN] Stable parameter name. [JA] 安定した parameter 名です。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>[EN] Standard dtype name for tensor-like parameters. [JA] tensor-like parameter 向けの標準 dtype 名です。</summary>
    public string DType { get; init; } = ComputeBufferDTypes.Unknown;

    /// <summary>[EN] Optional shape expression such as 1,256,256. [JA] 1,256,256 などの任意の shape expression です。</summary>
    public string? Shape { get; init; }

    /// <summary>[EN] Indicates whether the parameter is optional. [JA] parameter が optional かどうかを示します。</summary>
    public bool Optional { get; init; }

    /// <summary>[EN] Additional deterministic metadata. [JA] 追加の deterministic metadata です。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
