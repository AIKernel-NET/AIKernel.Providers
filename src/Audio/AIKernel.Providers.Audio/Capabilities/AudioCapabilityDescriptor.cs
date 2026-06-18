namespace AIKernel.Providers.Audio.Capabilities;

/// <summary>
/// [EN] Backend-independent audio capability descriptor.
/// [JA] backend 非依存の audio capability descriptor です。
/// </summary>
public sealed record AudioCapabilityDescriptor
{
    /// <summary>[EN] Provider identifier. [JA] Provider 識別子です。</summary>
    public string ProviderId { get; init; } = string.Empty;

    /// <summary>[EN] Indicates whether playback is supported. [JA] playback に対応しているかどうかを示します。</summary>
    public bool SupportsPlayback { get; init; }

    /// <summary>[EN] Indicates whether recording is supported. [JA] recording に対応しているかどうかを示します。</summary>
    public bool SupportsRecording { get; init; }

    /// <summary>[EN] Supported encoding names such as pcm16 or float32. [JA] pcm16 や float32 などの対応 encoding 名です。</summary>
    public IReadOnlyList<string> SupportedEncodings { get; init; } = [];

    /// <summary>[EN] Minimum supported sample rate in hertz. [JA] 対応する最小 sample rate hertz です。</summary>
    public int MinSampleRateHz { get; init; } = 8_000;

    /// <summary>[EN] Maximum supported sample rate in hertz. [JA] 対応する最大 sample rate hertz です。</summary>
    public int MaxSampleRateHz { get; init; } = 192_000;

    /// <summary>[EN] Minimum supported channel count. [JA] 対応する最小 channel count です。</summary>
    public int MinChannels { get; init; } = 1;

    /// <summary>[EN] Maximum supported channel count. [JA] 対応する最大 channel count です。</summary>
    public int MaxChannels { get; init; } = 8;

    /// <summary>[EN] Additional deterministic metadata. [JA] 追加の deterministic metadata です。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
