namespace AIKernel.Providers.Audio.Validation;

using AIKernel.Providers.Audio.Capabilities;
using AIKernel.Providers.Audio.Diagnostics;

/// <summary>
/// [EN] Validates provider-neutral audio formats against backend-independent capabilities.
/// [JA] backend 非依存 capability に対して Provider-neutral audio format を検証します。
/// </summary>
public sealed class AudioFormatValidator
{
    /// <summary>
    /// [EN] Validates an audio format.
    /// [JA] audio format を検証します。
    /// </summary>
    /// <param name="format">EN:  JA: format パラメーターです。
    /// [EN] Audio format.
    /// [JA] audio format です。
    /// </param>
    /// <param name="capability">EN:  JA: capability パラメーターです。
    /// [EN] Audio capability descriptor.
    /// [JA] audio capability descriptor です。
    /// </param>
    /// <returns>EN:  JA: 結果を返します。
    /// [EN] Structured validation result.
    /// [JA] 構造化された validation result です。
    /// </returns>
    public AudioFormatValidationResult Validate(
        AudioFormat? format,
        AudioCapabilityDescriptor? capability)
    {
        if (format is null)
        {
            return Failure("AUDIO_FORMAT_MISSING", "Audio format descriptor is required.");
        }

        if (capability is null)
        {
            return Failure("AUDIO_CAPABILITY_MISSING", "Audio capability descriptor is required.");
        }

        if (string.IsNullOrWhiteSpace(format.Encoding) ||
            capability.SupportedEncodings.Count > 0 &&
            !capability.SupportedEncodings.Contains(format.Encoding, StringComparer.OrdinalIgnoreCase))
        {
            return Failure(AudioProviderDiagnostics.FormatUnsupported, "Audio encoding is not supported.");
        }

        if (format.SampleRateHz < capability.MinSampleRateHz ||
            format.SampleRateHz > capability.MaxSampleRateHz)
        {
            return Failure(AudioProviderDiagnostics.FormatUnsupported, "Audio sample rate is not supported.");
        }

        if (format.Channels < capability.MinChannels ||
            format.Channels > capability.MaxChannels)
        {
            return Failure(AudioProviderDiagnostics.FormatUnsupported, "Audio channel count is not supported.");
        }

        if (format.BitsPerSample <= 0)
        {
            return Failure(AudioProviderDiagnostics.FormatUnsupported, "Audio bits per sample must be positive.");
        }

        return new AudioFormatValidationResult { Succeeded = true };
    }

    private static AudioFormatValidationResult Failure(
        string code,
        string message)
        => new()
        {
            Succeeded = false,
            ErrorCode = code,
            ErrorMessage = message,
            Diagnostics = [AudioProviderDiagnostics.Create(code, message)]
        };
}
