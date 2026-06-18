namespace AIKernel.Providers.Audio.Base;

/// <summary>
/// [EN] Structured result returned by audio recording operations.
/// [JA] audio recording operation が返す構造化 result です。
/// </summary>
public sealed record AudioRecordResult : AudioProviderResult
{
    /// <summary>[EN] Recorded audio payload bytes. [JA] recording された audio payload byte です。</summary>
    public IReadOnlyList<byte> Payload { get; init; } = [];

    /// <summary>[EN] Provider-neutral recorded audio frames. [JA] Provider-neutral な recording audio frame です。</summary>
    public IReadOnlyList<AudioFrame> Frames { get; init; } = [];

    /// <summary>[EN] Provider-neutral recorded audio frames with sample offsets. [JA] sample offset を持つ Provider-neutral な recording audio frame です。</summary>
    public IReadOnlyList<AudioRecordFrame> RecordFrames { get; init; } = [];
}
