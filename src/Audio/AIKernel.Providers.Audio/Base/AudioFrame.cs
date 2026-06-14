namespace AIKernel.Providers.Audio.Base;

/// <summary>
/// [EN] Provider-neutral audio frame with deterministic frame index and stream-relative timestamp.
/// [JA] deterministic frame index と stream-relative timestamp を持つ Provider-neutral な audio frame です。
/// </summary>
public sealed record AudioFrame
{
    /// <summary>[EN] Zero-based frame index within the stream. [JA] stream 内の zero-based frame index です。</summary>
    public long FrameIndex { get; init; }

    /// <summary>[EN] Stream-relative timestamp for the frame. [JA] frame の stream-relative timestamp です。</summary>
    public TimeSpan Timestamp { get; init; }

    /// <summary>[EN] Audio payload bytes for this frame. [JA] この frame の audio payload byte です。</summary>
    public IReadOnlyList<byte> Payload { get; init; } = [];

    /// <summary>[EN] Additional deterministic metadata. [JA] 追加の deterministic metadata です。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
