namespace AIKernel.Providers.Reranker;

/// <summary>[EN] Developer settings applied when creating a reranker instance. [JA] 再順位付けインスタンスの生成時に適用する開発者設定。</summary>
public sealed record LocalRerankerSettings
{
    /// <summary>[EN] Directory containing the pinned model files, or null when unregistered. [JA] 固定版モデルの配置先。未登録の場合はnull。</summary>
    public string? ModelDirectory { get; init; }

    /// <summary>[EN] Maximum candidates per request. [JA] リクエストあたりの最大候補数。</summary>
    public int MaxCandidates { get; init; } = 16;

    /// <summary>[EN] Maximum query tokens before adding pair separators. [JA] 組の区切りを付ける前のクエリの最大トークン数。</summary>
    public int MaxQueryTokens { get; init; } = 128;

    /// <summary>[EN] Maximum tokens per candidate before adding pair separators. [JA] 組の区切りを付ける前の候補1件の最大トークン数。</summary>
    public int MaxCandidateTokens { get; init; } = 380;

    /// <summary>[EN] Maximum combined tokens, including special tokens, up to 512. [JA] 特殊トークンを含む組の上限。最大512トークン。</summary>
    public int MaxPairTokens { get; init; } = MiniLmModel.ContextLength;

    /// <summary>[EN] Maximum number of pairs evaluated in one ONNX call. [JA] ONNXの1回の呼び出しで採点する最大件数。</summary>
    public int InferenceBatchSize { get; init; } = 4;

    /// <summary>[EN] ONNX intra-op threads. Zero uses the runtime default. [JA] ONNX演算内のスレッド数。0はランタイム既定値。</summary>
    public int CpuThreads { get; init; } = Math.Min(4, Environment.ProcessorCount);

    /// <summary>[EN] Optional UTF-8 byte limit for the query and all candidates combined. [JA] クエリと全候補を合計したUTF-8バイト数の上限。未指定時はnull。</summary>
    public long? MaxRequestBytes { get; init; }

    internal int EffectiveBatchSize => Math.Min(MaxCandidates, InferenceBatchSize);

    internal void Validate()
    {
        const int maxTextTokens = MiniLmModel.ContextLength - MiniLmModel.PairSpecialTokens - 1;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxCandidates);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(InferenceBatchSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxQueryTokens);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MaxQueryTokens, maxTextTokens);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxCandidateTokens);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MaxCandidateTokens, maxTextTokens);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxPairTokens, MiniLmModel.PairSpecialTokens + 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MaxPairTokens, MiniLmModel.ContextLength);
        ArgumentOutOfRangeException.ThrowIfNegative(CpuThreads);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MaxCandidates, Array.MaxLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((long)EffectiveBatchSize * MaxPairTokens, Array.MaxLength);
        if (MaxRequestBytes is <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxRequestBytes));
    }
}
