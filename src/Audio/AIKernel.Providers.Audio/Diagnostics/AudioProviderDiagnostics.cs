namespace AIKernel.Providers.Audio.Diagnostics;

using AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Factory helpers for backend-independent audio provider diagnostics.
/// [JA] backend 非依存の audio Provider diagnostic を作成する helper です。
/// </summary>
public static class AudioProviderDiagnostics
{
    /// <summary>[EN] Missing audio payload diagnostic code. [JA] audio payload 欠落 diagnostic code です。</summary>
    public const string PayloadMissing = "AUDIO_PAYLOAD_MISSING";

    /// <summary>[EN] Unsupported audio format diagnostic code. [JA] 未対応 audio format diagnostic code です。</summary>
    public const string FormatUnsupported = "AUDIO_FORMAT_UNSUPPORTED";

    /// <summary>[EN] Missing recording duration diagnostic code. [JA] recording duration 欠落 diagnostic code です。</summary>
    public const string DurationInvalid = "AUDIO_DURATION_INVALID";

    /// <summary>
    /// [EN] Creates an audio provider diagnostic.
    /// [JA] audio Provider diagnostic を作成します。
    /// </summary>
    /// <param name="code">EN:  JA: code パラメーターです。
    /// [EN] Diagnostic code.
    /// [JA] diagnostic code です。
    /// </param>
    /// <param name="message">EN:  JA: message パラメーターです。
    /// [EN] Diagnostic message.
    /// [JA] diagnostic message です。
    /// </param>
    /// <param name="severity">EN:  JA: severity パラメーターです。
    /// [EN] Diagnostic severity.
    /// [JA] diagnostic severity です。
    /// </param>
    /// <returns>EN:  JA: 結果を返します。
    /// [EN] Provider diagnostic.
    /// [JA] Provider diagnostic です。
    /// </returns>
    public static ProviderDiagnostic Create(
        string code,
        string message,
        string severity = "Error")
        => new()
        {
            Code = code,
            Message = message,
            Severity = severity
        };
}
