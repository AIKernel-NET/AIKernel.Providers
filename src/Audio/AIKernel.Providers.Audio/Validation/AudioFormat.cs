namespace AIKernel.Providers.Audio.Validation;

/// <summary>
/// [EN] Provider-neutral audio sample format.
/// [JA] Provider-neutral な audio sample format です。
/// </summary>
public sealed record AudioFormat
{
    /// <summary>[EN] Encoding name such as pcm16 or float32. [JA] pcm16 や float32 などの encoding 名です。</summary>
    public string Encoding { get; init; } = "pcm16";

    /// <summary>[EN] Sample rate in hertz. [JA] sample rate hertz です。</summary>
    public int SampleRateHz { get; init; } = 48_000;

    /// <summary>[EN] Channel count. [JA] channel count です。</summary>
    public int Channels { get; init; } = 2;

    /// <summary>[EN] Bits per sample. [JA] sample あたりの bit 数です。</summary>
    public int BitsPerSample { get; init; } = 16;

    /// <summary>[EN] Additional deterministic metadata. [JA] 追加の deterministic metadata です。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
