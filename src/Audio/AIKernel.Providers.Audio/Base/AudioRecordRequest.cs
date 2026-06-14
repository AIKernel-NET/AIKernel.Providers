namespace AIKernel.Providers.Audio.Base;

using AIKernel.Providers.Audio.Validation;

/// <summary>
/// [EN] Backend-independent audio recording request.
/// [JA] backend 非依存の audio recording request です。
/// </summary>
public sealed record AudioRecordRequest
{
    /// <summary>[EN] Operation identifier. [JA] operation 識別子です。</summary>
    public string OperationId { get; init; } = string.Empty;

    /// <summary>[EN] Provider-neutral audio sample format. [JA] Provider-neutral な audio sample format です。</summary>
    public AudioFormat Format { get; init; } = new();

    /// <summary>[EN] Requested recording duration. [JA] 要求された recording duration です。</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>[EN] Additional deterministic metadata. [JA] 追加の deterministic metadata です。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
