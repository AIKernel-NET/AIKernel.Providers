namespace AIKernel.Providers.Audio.Base;

using AIKernel.Providers.Audio.Capabilities;
using AIKernel.Providers.Audio.Diagnostics;
using AIKernel.Providers.Audio.Validation;

/// <summary>
/// [EN] Backend-independent base class for audio recording providers.
/// [JA] audio recording Provider 向けの backend 非依存 base class です。
/// </summary>
public abstract class AudioRecBase
{
    private readonly AudioFormatValidator _validator;

    /// <summary>
    /// [EN] Initializes the audio recording base class.
    /// [JA] audio recording base class を初期化します。
    /// </summary>
    /// <param name="capability">EN:  JA: capability パラメーターです。
    /// [EN] Audio capability descriptor.
    /// [JA] audio capability descriptor です。
    /// </param>
    /// <param name="validator">EN:  JA: validator パラメーターです。
    /// [EN] Audio format validator.
    /// [JA] audio format validator です。
    /// </param>
    protected AudioRecBase(
        AudioCapabilityDescriptor capability,
        AudioFormatValidator? validator = null)
    {
        Capability = capability ?? throw new ArgumentNullException(nameof(capability));
        _validator = validator ?? new AudioFormatValidator();
    }

    /// <summary>[EN] Gets the audio capability descriptor. [JA] audio capability descriptor を取得します。</summary>
    public AudioCapabilityDescriptor Capability { get; }

    /// <summary>
    /// [EN] Records audio after backend-independent request validation.
    /// [JA] backend 非依存の request validation 後に audio を record します。
    /// </summary>
    /// <param name="request">EN:  JA: request パラメーターです。
    /// [EN] Audio recording request.
    /// [JA] audio recording request です。
    /// </param>
    /// <param name="cancellationToken">EN:  JA: cancellationToken パラメーターです。
    /// [EN] Cancellation token.
    /// [JA] cancellation token です。
    /// </param>
    /// <returns>EN:  JA: 結果を返します。
    /// [EN] Structured audio recording result.
    /// [JA] 構造化された audio recording result です。
    /// </returns>
    public ValueTask<AudioRecordResult> RecordAsync(
        AudioRecordRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        if (!Capability.SupportsRecording)
        {
            return ValueTask.FromResult(Failure("AUDIO_RECORDING_UNSUPPORTED", "Audio recording is not supported."));
        }

        if (request.Duration <= TimeSpan.Zero)
        {
            return ValueTask.FromResult(Failure(AudioProviderDiagnostics.DurationInvalid, "Audio recording duration must be positive."));
        }

        var validation = _validator.Validate(request.Format, Capability);
        return validation.Succeeded
            ? RecordCoreAsync(request, cancellationToken)
            : ValueTask.FromResult(Failure(validation.ErrorCode!, validation.ErrorMessage!));
    }

    /// <summary>
    /// [EN] Performs provider recording after validation has succeeded.
    /// [JA] validation 成功後に Provider recording を実行します。
    /// </summary>
    /// <param name="request">EN:  JA: request パラメーターです。
    /// [EN] Audio recording request.
    /// [JA] audio recording request です。
    /// </param>
    /// <param name="cancellationToken">EN:  JA: cancellationToken パラメーターです。
    /// [EN] Cancellation token.
    /// [JA] cancellation token です。
    /// </param>
    /// <returns>EN:  JA: 結果を返します。
    /// [EN] Structured audio recording result.
    /// [JA] 構造化された audio recording result です。
    /// </returns>
    protected abstract ValueTask<AudioRecordResult> RecordCoreAsync(
        AudioRecordRequest request,
        CancellationToken cancellationToken);

    private static AudioRecordResult Failure(
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
