using System.Collections.ObjectModel;
using AIKernel.Abstractions.Models;
using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Core;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Providers;
using AIKernel.Dtos.Providers.SpeechRecognition;
using AIKernel.Dtos.Routing;

namespace AIKernel.Providers.Stt;

/// <summary>[EN] Local CPU WAV transcription with the pinned multilingual Whisper small. [JA] 固定版の多言語Whisper smallを使うCPU文字起こし。</summary>
public sealed class LocalSpeechRecognitionProvider : ISpeechRecognitionProvider, IAsyncDisposable
{
    private const string LocalId = "asr.local";
    private const string WaveMediaType = "audio/wav";
    private static readonly IReadOnlyList<string> Languages = Array.AsReadOnly(new[] { "ja", "en" });
    private static readonly IReadOnlyList<string> ModelIds = Array.AsReadOnly(new[] { WhisperModel.ModelId });
    private static readonly IReadOnlyList<string> Operations = Array.AsReadOnly(new[] { LocalId });
    private static readonly IReadOnlyList<string> DataTypes = Array.AsReadOnly(new[] { WaveMediaType });
    private static readonly IReadOnlyDictionary<string, string> Provenance =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["model_revision"] = WhisperModel.ModelRevision,
            ["weights_sha256"] = WhisperModel.ModelSha256,
            ["whisper_cpp_version"] = WhisperModel.NativeVersion
        });
    private static readonly IReadOnlyDictionary<string, string> NoMetadata =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

    private readonly LocalSpeechRecognitionSettings settings;
    private readonly string? modelPath;
    private readonly SemaphoreSlim gate = new(1, 1);
    private WhisperModel? model;
    private bool disposed;

    /// <summary>[EN] Creates a configured or unregistered provider. [JA] 設定済みまたはモデル未登録のProviderを生成します。</summary>
    public LocalSpeechRecognitionProvider(LocalSpeechRecognitionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        this.settings = settings;
        modelPath = string.IsNullOrWhiteSpace(settings.ModelPath) ? null : Path.GetFullPath(settings.ModelPath);
    }

    /// <inheritdoc />
    public string ProviderId => LocalId;

    /// <inheritdoc />
    public string Name => "AIKernel Local Speech Recognition";

    /// <inheritdoc />
    public string Version => global::AIKernel.Providers.ProviderBuildVersion.Value;

    private bool Available => Volatile.Read(ref model) is not null;

    /// <inheritdoc />
    public Task<bool> IsAvailableAsync() => Task.FromResult(Available);

    /// <inheritdoc />
    public Task<ProviderHealthStatus> GetHealthAsync()
    {
        bool available = Available;
        return Task.FromResult(new ProviderHealthStatus(available,
            available ? "The pinned Whisper small model is loaded." : "The speech model is not loaded.", DateTime.UtcNow, 0));
    }

    /// <inheritdoc />
    public IProviderCapabilities GetCapabilities() => new Capabilities(Available);

    /// <inheritdoc />
    public ProviderCapabilityDescriptor GetCapabilityDescriptor()
    {
        bool available = Available;
        long monoBytes = (long)(settings.MaxAudioDuration.TotalSeconds * (Pcm16Wave.MaxSampleRate + Pcm16Wave.ModelSampleRate) * sizeof(float));
        return new()
        {
            SchemaVersion = ProviderPlacementAbi.SchemaVersion,
            ProviderId = ProviderId,
            Kind = ProviderKind.SpeechRecognition,
            ModelIds = available ? ModelIds : [],
            InputModalities = [ProviderModality.Audio],
            ExecutionTargets = [ExecutionTarget.Cpu],
            Modes = ExecutionMode.Single,
            Precisions = ["FP16"],
            Memory = new() { WorkingSetBytes = 950_000_000 + monoBytes, Basis = "estimated" },
            Limits = new() { MaxItemsPerRequest = 1, MaxRequestBytes = settings.MaxRequestBytes },
            Extension = new SpeechRecognitionCapabilityExtension
            {
                Languages = Languages,
                SupportsLanguageDetection = true,
                TimestampGranularities = [TimestampGranularity.None, TimestampGranularity.Segment],
                AcceptedMediaTypes = DataTypes,
                VadModes = [VadMode.Off],
                MaxAudioDuration = settings.MaxAudioDuration
            },
            Metadata = available ? Provenance : NoMetadata
        };
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (model is not null || modelPath is null)
                return;
            try
            {
                var loaded = await Task.Run(() => new WhisperModel(modelPath)).ConfigureAwait(false);
                Volatile.Write(ref model, loaded);
            }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or
                DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or PlatformNotSupportedException)
            {
                throw Error(ProviderErrorCodes.ExecutionFailed, "The pinned Whisper model or CPU runtime could not be loaded.");
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<TranscriptionResult> TranscribeAsync(TranscriptionRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request is null || request.Audio is null)
            throw Error(ProviderErrorCodes.InvalidRequest, "An audio input is required.");
        if (request.ModelId is not (null or WhisperModel.ModelId))
            throw NotRegistered();
        string? language = ResolveLanguage(request.Language);
        ValidateOptions(request);

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var loaded = model ?? throw NotRegistered();
            var wave = await Pcm16Wave.ReadAsync(request.Audio, settings, cancellationToken).ConfigureAwait(false);
            return await Task.Run(() => Recognize(loaded, wave, language, request.Timestamps, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private TranscriptionResult Recognize(WhisperModel loaded, Pcm16Wave wave, string? language,
        TimestampGranularity? timestamps, CancellationToken ct)
    {
        TranscriptionSegment[] segments = [];
        if (wave.Samples.AsSpan().ContainsAnyExcept(0f))
        {
            segments = loaded.Transcribe(wave, language, settings.CpuThreads, ct, out language);
            if (segments.Length != 0 && !Languages.Contains(language))
                throw UnknownLanguage();
        }
        ct.ThrowIfCancellationRequested();
        return new()
        {
            ProviderId = ProviderId,
            ModelId = WhisperModel.ModelId,
            Language = Languages.Contains(language) ? language : null,
            Duration = wave.Duration,
            Text = string.Join(language == "ja" ? string.Empty : " ", segments.Select(segment => segment.Text)),
            Segments = timestamps == TimestampGranularity.None ? [] : Array.AsReadOnly(segments)
        };
    }

    private static string? ResolveLanguage(string? language) => language?.ToLowerInvariant() switch
    {
        null => null,
        "ja" => "ja",
        "en" => "en",
        _ => throw UnknownLanguage()
    };

    private static void ValidateOptions(TranscriptionRequest request)
    {
        if (request.SpeechRegions is not null && request.Vad != VadMode.External ||
            request.Vad == VadMode.External && request.SpeechRegions is not { Count: > 0 })
            throw Error(ProviderErrorCodes.InvalidRequest, "Speech regions are required only with external VAD.");
        if (request.Timestamps is not (null or TimestampGranularity.None or TimestampGranularity.Segment) ||
            request.Vad is not (null or VadMode.Off))
            throw Error(ProviderErrorCodes.UnsupportedOption, "The requested timestamp or VAD mode is unsupported.");
        string? mediaType = request.Audio switch
        {
            AudioSource.Bytes bytes => bytes.MediaType,
            AudioSource.FromStream stream => stream.MediaType,
            _ => null
        };
        if (!StringComparer.OrdinalIgnoreCase.Equals(mediaType, WaveMediaType))
            throw Error(ProviderErrorCodes.UnsupportedFormat, "The provider accepts PCM16 WAV audio.");
    }

    /// <inheritdoc />
    public Task ShutdownAsync() => ReleaseAsync(permanent: false).AsTask();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ReleaseAsync(permanent: true);

    private async ValueTask ReleaseAsync(bool permanent)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            disposed |= permanent;
            Interlocked.Exchange(ref model, null)?.Dispose();
        }
        finally
        {
            gate.Release();
        }
    }

    private static ProviderOperationException NotRegistered() =>
        Error(ProviderErrorCodes.ModelNotRegistered, "The requested speech recognition model is not registered.");

    private static ProviderOperationException UnknownLanguage() =>
        Error(SpeechRecognitionErrorCodes.UnsupportedLanguage, "The speech recognition provider supports Japanese and English.");

    internal static ProviderOperationException Error(string code, string message) => new(new ExecutionError(code, message));

    private sealed class Capabilities(bool available) : IProviderCapabilities
    {
        public IReadOnlyList<string> SupportedOperations => available ? Operations : [];
        public IReadOnlyList<string> SupportedDataTypes => available ? DataTypes : [];
        public int MaxConcurrentConnections => 1;
        public RateLimitInfo? RateLimit => null;
        public ModelCapacityVector Vector => new();
        public IDictionary<string, float>? GetDynamicCapacities(IExecutionConstraints constraints) => null;
        public ICapabilityProfile? GetCapabilityProfile() => null;
        public bool SupportsOperation(string operation) => available && StringComparer.OrdinalIgnoreCase.Equals(operation, LocalId);
        public bool SupportsDataType(string dataType) => available && StringComparer.OrdinalIgnoreCase.Equals(dataType, WaveMediaType);
        public bool SupportsQuantization(string quantizationLevel) =>
            available && StringComparer.OrdinalIgnoreCase.Equals(quantizationLevel, "FP16");
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
