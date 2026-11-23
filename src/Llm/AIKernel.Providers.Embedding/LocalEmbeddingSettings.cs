using AIKernel.Dtos.Providers.Embedding;

namespace AIKernel.Providers.Embedding;

/// <summary>[EN] Developer-configurable settings for an embedding provider instance. [JA] インスタンスごとに開発者が指定する埋め込み設定。</summary>
public sealed record LocalEmbeddingSettings
{
    /// <summary>[EN] Directory containing the pinned model files, or null when unregistered. [JA] 固定モデルのディレクトリ。未登録の場合はnull。</summary>
    public string? ModelDirectory { get; init; }

    /// <summary>[EN] Maximum input count for one request. [JA] 1回の要求で受け付ける最大件数。</summary>
    public int MaxBatchSize { get; init; } = 16;

    /// <summary>[EN] Maximum tokens including the prefix and special tokens, up to 512. [JA] 接頭辞・特殊トークン込みの上限。最大512。</summary>
    public int MaxTokens { get; init; } = 512;

    /// <summary>[EN] Optional UTF-8 byte limit for the input texts of one request. [JA] 1回の入力テキスト全体のUTF-8バイト上限。未指定時はnull。</summary>
    public long? MaxRequestBytes { get; init; }

    /// <summary>[EN] ONNX intra-op thread count. Zero uses the runtime default. [JA] ONNX演算内のスレッド数。0はランタイム既定値。</summary>
    public int CpuThreads { get; init; } = Math.Min(4, Environment.ProcessorCount);

    /// <summary>[EN] Default for per-request normalization. [JA] 正規化を指定しない場合の既定値。</summary>
    public bool DefaultNormalize { get; init; } = true;

    /// <summary>[EN] Default for per-request retrieval purpose. [JA] 検索用途を指定しない場合の既定値。</summary>
    public EmbeddingPurpose DefaultPurpose { get; init; } = EmbeddingPurpose.Query;

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxBatchSize);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxTokens, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MaxTokens, E5Model.ContextLength);
        ArgumentOutOfRangeException.ThrowIfNegative(CpuThreads);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((long)MaxBatchSize * MaxTokens * E5Model.Dimensions, Array.MaxLength);
        if (MaxRequestBytes is <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxRequestBytes));
        if (!Enum.IsDefined(DefaultPurpose))
            throw new ArgumentOutOfRangeException(nameof(DefaultPurpose));
    }
}
