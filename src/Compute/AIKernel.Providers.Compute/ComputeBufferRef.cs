namespace AIKernel.Providers.Compute;

/// <summary>
/// [EN] Backend-neutral reference to tensor-like compute buffer data.
/// [JA] tensor-like compute buffer data への backend-neutral な参照です。
/// </summary>
public sealed record ComputeBufferRef
{
    /// <summary>[EN] Buffer identifier assigned by the orchestration boundary. [JA] orchestration 境界が割り当てる buffer identifier です。</summary>
    public string BufferId { get; init; } = string.Empty;

    /// <summary>[EN] Standard dtype name such as f32, f16, i32, or u8. [JA] f32、f16、i32、u8 などの標準 dtype 名です。</summary>
    public string DType { get; init; } = ComputeBufferDTypes.Unknown;

    /// <summary>[EN] Shape encoded as a comma-separated string such as 1,256,256. [JA] 1,256,256 などの comma-separated string として encode した shape です。</summary>
    public string Shape { get; init; } = string.Empty;

    /// <summary>[EN] Optional stride encoded as a comma-separated string. [JA] comma-separated string として encode した任意の stride です。</summary>
    public string? Stride { get; init; }

    /// <summary>[EN] Optional layout name such as contiguous or channels-last. [JA] contiguous や channels-last などの任意の layout 名です。</summary>
    public string? Layout { get; init; }

    /// <summary>[EN] Optional hash metadata for the referenced buffer. [JA] 参照 buffer 向けの任意の hash metadata です。</summary>
    public HashMetadata Hash { get; init; } = new();

    /// <summary>[EN] Backend-specific metadata carried outside the standard fields. [JA] 標準 field の外側に保持する backend-specific metadata です。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// [EN] Projects the buffer reference to deterministic metadata keys.
    /// [JA] buffer reference を deterministic metadata key へ射影します。
    /// </summary>
    /// <param name="prefix">
    /// [EN] Metadata key prefix.
    /// [JA] metadata key prefix です。
    /// </param>
    /// <returns>
    /// [EN] Deterministic metadata map.
    /// [JA] deterministic metadata map です。
    /// </returns>
    public IReadOnlyDictionary<string, string> ToMetadata(string prefix = "compute.buffer")
    {
        var metadata = new SortedDictionary<string, string>(StringComparer.Ordinal);
        AddIfPresent(metadata, $"{prefix}.id", BufferId);
        AddIfPresent(metadata, $"{prefix}.dtype", DType);
        AddIfPresent(metadata, $"{prefix}.shape", Shape);
        AddIfPresent(metadata, $"{prefix}.stride", Stride);
        AddIfPresent(metadata, $"{prefix}.layout", Layout);
        AddIfPresent(metadata, $"{prefix}.hash.algorithm", Hash.Algorithm);
        AddIfPresent(metadata, $"{prefix}.hash.value", Hash.Value);
        AddIfPresent(metadata, $"{prefix}.hash.expression", Hash.Expression);

        foreach (var item in Metadata.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            metadata[$"{prefix}.metadata.{item.Key}"] = item.Value;
        }

        return metadata;
    }

    private static void AddIfPresent(
        SortedDictionary<string, string> metadata,
        string key,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            metadata[key] = value;
        }
    }
}
