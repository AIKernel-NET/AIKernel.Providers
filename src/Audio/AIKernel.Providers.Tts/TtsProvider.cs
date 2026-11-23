using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using AIKernel.Abstractions.Capabilities;
using AIKernel.Abstractions.Models;
using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Capabilities;
using AIKernel.Dtos.Core;
using AIKernel.Dtos.Routing;
using AIKernel.Enums;

namespace AIKernel.Providers.Tts;

/// <summary>Invokes an installed text-to-speech service through the AIKernel capability contract.</summary>
public sealed class TtsProvider(TtsSettings settings) : IProvider, ICapabilityModuleInvoker
{
    /// <summary>The provider and capability module identifier.</summary>
    public const string CapabilityId = "tts";

    /// <summary>The operation that turns text into PCM WAV audio.</summary>
    public const string SynthesizeOperation = "tts.synthesize";

    private const string Protocol = "aikernel-tts-http/v1";
    private static readonly IProviderCapabilities Capabilities = new TtsCapabilities();
    private readonly TtsSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    private volatile bool _initialized;

    /// <summary>Creates an unconfigured provider for discovery.</summary>
    public TtsProvider() : this(new TtsSettings()) { }

    /// <inheritdoc />
    public string ProviderId => CapabilityId;

    /// <inheritdoc />
    public string Name => "Text-to-Speech";

    /// <inheritdoc />
    public string Version => global::AIKernel.Providers.ProviderBuildVersion.Value;

    /// <inheritdoc />
    public IProviderCapabilities GetCapabilities() => Capabilities;

    /// <inheritdoc />
    public Task InitializeAsync()
    {
        _initialized = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ShutdownAsync()
    {
        _initialized = false;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync() => (await GetHealthAsync().ConfigureAwait(false)).IsHealthy;

    /// <inheritdoc />
    public async Task<ProviderHealthStatus> GetHealthAsync()
    {
        var started = Stopwatch.GetTimestamp();
        var issue = ConfigurationIssue();
        if (issue is not null)
            return Health(issue);

        using var deadline = new CancellationTokenSource(_settings.Timeout);
        try
        {
            using var response = await TtsRuntimeClient.HealthAsync(_settings.Endpoint!, deadline.Token)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return Health(HttpIssue(response));

            var status = await TtsRuntimeClient.ReadHealthAsync(response, deadline.Token).ConfigureAwait(false);
            if (status?.Engine != _settings.Engine || status.Languages is not { Length: > 0 }
                || status.Voices is not { Length: > 0 })
                return Health(("TTS_INVALID_RESPONSE", "The runtime did not report the expected engine and capabilities."));
            if (!status.Languages.Contains(_settings.Language, StringComparer.OrdinalIgnoreCase))
                return Health(("TTS_UNSUPPORTED_LANGUAGE", "The runtime does not support the configured language."));
            if (_settings.Voice is not null && !status.Voices.Contains(_settings.Voice, StringComparer.Ordinal))
                return Health(("TTS_UNSUPPORTED_VOICE", "The runtime does not provide the configured voice."));

            return Health(null);
        }
        catch (Exception ex) when (TransportIssue(ex) is not null)
        {
            return Health(TransportIssue(ex));
        }

        ProviderHealthStatus Health((string Code, string Message)? failure)
            => new(failure is null, failure is { } error ? $"{error.Code}: {error.Message}" : "TTS runtime ready.",
                DateTime.UtcNow, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }

    /// <summary>Provides the descriptor to register with <see cref="ICapabilityModuleRegistry"/>.</summary>
    public CapabilityModuleDescriptor ToCapabilityDescriptor()
        => new(CapabilityId, Name, CapabilityModuleKind.ManagedAssembly, CapabilityInvocationMode.AssemblyReference,
            Version, typeof(TtsProvider).FullName, null, null, [SynthesizeOperation], ["network.egress"],
            new Dictionary<string, string>(StringComparer.Ordinal) { ["protocol"] = Protocol, ["engine"] = _settings.Engine });

    /// <inheritdoc />
    public async ValueTask<CapabilityInvocationResult> InvokeAsync(
        CapabilityInvocationRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        if (request.CapabilityId != CapabilityId || request.Operation != SynthesizeOperation)
            return Failure(("TTS_OPERATION_NOT_SUPPORTED", "Use capability tts and operation tts.synthesize."));
        if (ConfigurationIssue() is { } issue)
            return Failure(issue);

        var arguments = request.Arguments;
        if (!arguments.TryGetValue("text", out var text) || string.IsNullOrWhiteSpace(text) || text.Length > 4096)
            return Failure(("TTS_INVALID_ARGUMENT", "The text argument must contain 1 to 4096 UTF-16 code units."));
        var language = arguments.GetValueOrDefault("language", _settings.Language);
        var voice = arguments.TryGetValue("voice", out var requestedVoice) ? requestedVoice : _settings.Voice;
        if (!IsToken(language))
            return Failure(("TTS_INVALID_ARGUMENT", "The language must be a language tag."));
        if (!IsToken(voice))
            return Failure(("TTS_INVALID_ARGUMENT", "Specify a stock voice ID in the request or settings."));
        double speed = 1;
        if (arguments.TryGetValue("speed", out var value)
            && (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out speed)
                || !double.IsFinite(speed) || speed is < 0.5 or > 2))
            return Failure(("TTS_INVALID_ARGUMENT", "The speed must be between 0.5 and 2."));

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_settings.Timeout);
        try
        {
            using var response = await TtsRuntimeClient.SynthesizeAsync(
                _settings.Endpoint!, new TtsRuntimeRequest(text, language!, voice!, speed), deadline.Token)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!response.IsSuccessStatusCode)
                return Failure(HttpIssue(response));

            var engine = TtsRuntimeClient.Header(response, "X-Tts-Engine");
            var actualVoice = TtsRuntimeClient.Header(response, "X-Tts-Voice");
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (engine != _settings.Engine || actualVoice != voice ||
                !(string.Equals(mediaType, "audio/wav", StringComparison.OrdinalIgnoreCase)
                  || string.Equals(mediaType, "audio/x-wav", StringComparison.OrdinalIgnoreCase)))
                return Failure(("TTS_INVALID_RESPONSE", "The runtime returned an unexpected engine, voice or format."));

            var wave = await TtsRuntimeClient.ReadBytesAsync(response, TtsRuntimeClient.MaxWaveBytes, deadline.Token)
                .ConfigureAwait(false);
            deadline.Token.ThrowIfCancellationRequested();
            if (!PcmWave.TryRead(wave, out var format))
                return Failure(("TTS_INVALID_RESPONSE", "The runtime returned an invalid PCM WAV file."));

            var hash = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(wave));
            var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["provider"] = CapabilityId,
                ["engine"] = engine!,
                ["voice"] = actualVoice!,
                ["format"] = "wav",
                ["sample_rate_hz"] = format.SampleRate.ToString(CultureInfo.InvariantCulture),
                ["channels"] = format.Channels.ToString(CultureInfo.InvariantCulture),
                ["sample_frames"] = format.Frames.ToString(CultureInfo.InvariantCulture),
                ["audio_base64"] = Convert.ToBase64String(wave)
            };
            deadline.Token.ThrowIfCancellationRequested();
            return new(request.InvocationId, request.CapabilityId, true, hash, null, null, request.ReplayLogHash, metadata);
        }
        catch (Exception ex) when (TransportIssue(ex) is { } error)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Failure(error);
        }

        CapabilityInvocationResult Failure((string Code, string Message) error)
            => new(request.InvocationId, request.CapabilityId, false, null, error.Code, error.Message, request.ReplayLogHash,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["provider"] = CapabilityId });
    }

    private (string Code, string Message)? ConfigurationIssue()
    {
        var endpoint = _settings.Endpoint;
        if (endpoint is null)
            return ("TTS_NOT_CONFIGURED", "Configure the runtime endpoint before calling the TTS provider.");
        if (!endpoint.IsAbsoluteUri || !(endpoint.Scheme == "https" || endpoint.Scheme == "http" && endpoint.IsLoopback)
            || endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0
            || !endpoint.AbsolutePath.EndsWith('/') || !IsToken(_settings.Engine) || !IsToken(_settings.Language)
            || (_settings.Voice is not null && !IsToken(_settings.Voice))
            || _settings.Timeout <= TimeSpan.Zero || _settings.Timeout > TimeSpan.FromMinutes(10))
            return ("TTS_INVALID_CONFIGURATION", "The runtime endpoint or TTS settings are invalid.");
        return _initialized ? null : ("TTS_NOT_INITIALIZED", "Initialize the TTS provider before making requests.");
    }

    private static bool IsToken(string? value)
        => value is { Length: > 0 and <= 96 }
            && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');

    private static (string Code, string Message) HttpIssue(HttpResponseMessage response)
        => (response.StatusCode, TtsRuntimeClient.Header(response, "X-Tts-Error")) switch
        {
            (HttpStatusCode.UnprocessableEntity, "unsupported_language") =>
                ("TTS_UNSUPPORTED_LANGUAGE", "The runtime does not support the requested language."),
            (HttpStatusCode.UnprocessableEntity, "unsupported_voice") =>
                ("TTS_UNSUPPORTED_VOICE", "The runtime does not provide the requested voice."),
            (HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout, _) =>
                ("TTS_TIMEOUT", "The runtime did not respond within the deadline."),
            (HttpStatusCode.NotFound or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable, _) =>
                ("TTS_ENDPOINT_UNAVAILABLE", "The configured runtime or endpoint is unavailable."),
            _ => ("TTS_RUNTIME_FAILURE", $"The runtime returned HTTP {(int)response.StatusCode}.")
        };

    private static (string Code, string Message)? TransportIssue(Exception ex) => ex switch
    {
        OperationCanceledException => ("TTS_TIMEOUT", "The runtime did not respond within the deadline."),
        InvalidDataException or JsonException => ("TTS_INVALID_RESPONSE", "The runtime returned invalid or oversized data."),
        HttpRequestException or IOException => ("TTS_ENDPOINT_UNAVAILABLE", "The configured runtime connection failed."),
        _ => null
    };

    private sealed class TtsCapabilities : IProviderCapabilities
    {
        public IReadOnlyList<string> SupportedOperations => [SynthesizeOperation];
        public IReadOnlyList<string> SupportedDataTypes => ["text", "audio"];
        public int MaxConcurrentConnections => 1;
        public RateLimitInfo? RateLimit => null;
        public ModelCapacityVector Vector => new();
        public IDictionary<string, float>? GetDynamicCapacities(IExecutionConstraints constraints) => null;
        public ICapabilityProfile? GetCapabilityProfile() => null;
        public bool SupportsOperation(string operation) => operation == SynthesizeOperation;
        public bool SupportsDataType(string dataType) => dataType is "text" or "audio";
        public bool SupportsQuantization(string quantizationLevel) => false;
        public bool SupportsQueryAugmentation => false;
        public bool SupportsQueryDecomposition => false;
        public bool SupportsQueryRouting => false;
        public int MaxQueryParts => 0;
        public IReadOnlyList<string> SupportedQueryProcessingOperations => [];
        public bool SupportsQueryProcessingOperation(string operation) => false;
        public bool SupportsEmbedding => false;
        public int? EmbeddingDimensions => null;
        public IReadOnlyList<string> SupportedEmbeddingModels => [];
    }
}
