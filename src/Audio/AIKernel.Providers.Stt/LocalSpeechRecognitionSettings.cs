namespace AIKernel.Providers.Stt;

/// <summary>[EN] Local batch transcription settings. [JA] ローカル音声文字起こしの設定。</summary>
public sealed record LocalSpeechRecognitionSettings
{
    /// <summary>[EN] Path to the pinned Whisper small model. Null leaves the model unregistered. [JA] 固定版Whisper smallのパス。nullは未登録。</summary>
    public string? ModelPath { get; init; }

    /// <summary>[EN] Maximum duration of the original WAV. [JA] 元のWAVの最大音声長。</summary>
    public TimeSpan MaxAudioDuration { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>[EN] Maximum number of encoded WAV bytes, including its headers. [JA] ヘッダーを含めたWAVファイルの最大バイト数。</summary>
    public long MaxRequestBytes { get; init; } = 128L * 1024 * 1024;

    /// <summary>[EN] Whisper CPU threads. Zero uses the native default. [JA] WhisperのCPUスレッド数。0はネイティブの既定値。</summary>
    public int CpuThreads { get; init; } = Math.Min(4, Environment.ProcessorCount);

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(MaxAudioDuration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MaxAudioDuration, TimeSpan.FromSeconds(Array.MaxLength / Pcm16Wave.MaxSampleRate));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxRequestBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(CpuThreads);
    }
}
