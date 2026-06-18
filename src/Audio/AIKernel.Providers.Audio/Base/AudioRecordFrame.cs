namespace AIKernel.Providers.Audio.Base;

/// <summary>
/// [EN] Provider-neutral audio recording frame with deterministic position metadata.
/// [JA] deterministic position metadata を持つ Provider-neutral な audio recording frame です。
/// </summary>
public sealed record AudioRecordFrame
{
    /// <summary>[EN] Zero-based frame index within the recording stream. [JA] recording stream 内の zero-based frame index です。</summary>
    public long FrameIndex { get; init; }

    /// <summary>[EN] Zero-based sample offset within the recording stream. [JA] recording stream 内の zero-based sample offset です。</summary>
    public long SampleOffset { get; init; }

    /// <summary>[EN] Stream-relative timestamp for the frame. [JA] frame の stream-relative timestamp です。</summary>
    public TimeSpan Timestamp { get; init; }

    /// <summary>[EN] Audio payload bytes for this frame. [JA] この frame の audio payload byte です。</summary>
    public IReadOnlyList<byte> Payload { get; init; } = [];

    /// <summary>[EN] Additional deterministic metadata. [JA] 追加の deterministic metadata です。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
