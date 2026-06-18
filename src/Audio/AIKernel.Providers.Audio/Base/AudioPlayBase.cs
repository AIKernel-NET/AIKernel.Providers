namespace AIKernel.Providers.Audio.Base;

using AIKernel.Providers.Audio.Capabilities;
using AIKernel.Providers.Audio.Diagnostics;
using AIKernel.Providers.Audio.Validation;

/// <summary>
/// [EN] Backend-independent base class for audio playback providers.
/// [JA] audio playback Provider 向けの backend 非依存 base class です。
/// </summary>
public abstract class AudioPlayBase
{
    private readonly AudioFormatValidator _validator;

    /// <summary>
    /// [EN] Initializes the audio playback base class.
    /// [JA] audio playback base class を初期化します。
    /// </summary>
    /// <param name="capability">
    /// [EN] Audio capability descriptor.
    /// [JA] audio capability descriptor です。
    /// </param>
    /// <param name="validator">
    /// [EN] Audio format validator.
    /// [JA] audio format validator です。
    /// </param>
    protected AudioPlayBase(
        AudioCapabilityDescriptor capability,
        AudioFormatValidator? validator = null)
    {
        Capability = capability ?? throw new ArgumentNullException(nameof(capability));
        _validator = validator ?? new AudioFormatValidator();
    }

    /// <summary>[EN] Gets the audio capability descriptor. [JA] audio capability descriptor を取得します。</summary>
    public AudioCapabilityDescriptor Capability { get; }

    /// <summary>
    /// [EN] Plays audio after backend-independent request validation.
    /// [JA] backend 非依存の request validation 後に audio を playback します。
    /// </summary>
    /// <param name="request">
    /// [EN] Audio playback request.
    /// [JA] audio playback request です。
    /// </param>
    /// <param name="cancellationToken">
    /// [EN] Cancellation token.
    /// [JA] cancellation token です。
    /// </param>
    /// <returns>
    /// [EN] Structured audio provider result.
    /// [JA] 構造化された audio Provider result です。
    /// </returns>
    public ValueTask<AudioProviderResult> PlayAsync(
        AudioPlayRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        if (!Capability.SupportsPlayback)
        {
            return ValueTask.FromResult(Failure("AUDIO_PLAYBACK_UNSUPPORTED", "Audio playback is not supported."));
        }

        if (request.Payload.Count == 0)
        {
            return ValueTask.FromResult(Failure(AudioProviderDiagnostics.PayloadMissing, "Audio playback payload is required."));
        }

        var validation = _validator.Validate(request.Format, Capability);
        return validation.Succeeded
            ? PlayCoreAsync(request, cancellationToken)
            : ValueTask.FromResult(Failure(validation.ErrorCode!, validation.ErrorMessage!));
    }

    /// <summary>
    /// [EN] Performs provider playback after validation has succeeded.
    /// [JA] validation 成功後に Provider playback を実行します。
    /// </summary>
    /// <param name="request">
    /// [EN] Audio playback request.
    /// [JA] audio playback request です。
    /// </param>
    /// <param name="cancellationToken">
    /// [EN] Cancellation token.
    /// [JA] cancellation token です。
    /// </param>
    /// <returns>
    /// [EN] Structured audio provider result.
    /// [JA] 構造化された audio Provider result です。
    /// </returns>
    protected abstract ValueTask<AudioProviderResult> PlayCoreAsync(
        AudioPlayRequest request,
        CancellationToken cancellationToken);

    private static AudioProviderResult Failure(
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
