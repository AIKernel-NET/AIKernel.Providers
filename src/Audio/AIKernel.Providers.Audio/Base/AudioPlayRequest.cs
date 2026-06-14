namespace AIKernel.Providers.Audio.Base;

using AIKernel.Providers.Audio.Validation;

/// <summary>
/// [EN] Backend-independent audio playback request.
/// [JA] backend 非依存の audio playback request です。
/// </summary>
public sealed record AudioPlayRequest
{
    /// <summary>[EN] Operation identifier. [JA] operation 識別子です。</summary>
    public string OperationId { get; init; } = string.Empty;

    /// <summary>[EN] Provider-neutral audio sample format. [JA] Provider-neutral な audio sample format です。</summary>
    public AudioFormat Format { get; init; } = new();

    /// <summary>[EN] Audio payload bytes. [JA] audio payload byte です。</summary>
    public IReadOnlyList<byte> Payload { get; init; } = [];

    /// <summary>[EN] Additional deterministic metadata. [JA] 追加の deterministic metadata です。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
