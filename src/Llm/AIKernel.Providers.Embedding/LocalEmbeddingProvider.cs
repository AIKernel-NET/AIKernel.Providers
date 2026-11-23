using System.Collections.ObjectModel;
using System.Text;
using AIKernel.Abstractions.Models;
using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Core;
using AIKernel.Dtos.Providers;
using AIKernel.Dtos.Providers.Embedding;
using AIKernel.Dtos.Routing;
using OnnxRuntimeException = Microsoft.ML.OnnxRuntime.OnnxRuntimeException;

namespace AIKernel.Providers.Embedding;

/// <summary>[EN] Local CPU multilingual E5 embedding provider. [JA] CPUで動作するローカル多言語E5埋め込みProvider。</summary>
public sealed class LocalEmbeddingProvider : IMultimodalEmbeddingProvider, IAsyncDisposable
{
    private const string LocalId = "embedding.local";
    private static readonly IReadOnlyList<string> ModelIds = Array.AsReadOnly(new[] { E5Model.ModelId });
    private static readonly IReadOnlyList<string> Operations = Array.AsReadOnly(new[] { LocalId });
    private static readonly IReadOnlyList<string> DataTypes = Array.AsReadOnly(new[] { "text", "embedding" });
    private static readonly IReadOnlyDictionary<string, string> Provenance =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["model_revision"] = E5Model.Revision,
            ["weights_sha256"] = E5Model.WeightsSha256,
            ["tokenizer_sha256"] = E5Model.TokenizerSha256
        });

    private readonly LocalEmbeddingSettings settings;
    private readonly string? modelDirectory;
    private readonly SemaphoreSlim gate = new(1, 1);
    private E5Model? model;
    private bool disposed;

    /// <summary>[EN] Creates an unregistered or configured provider. [JA] 未登録またはモデル登録済みの設定でProviderを作成します。</summary>
    public LocalEmbeddingProvider(LocalEmbeddingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        this.settings = settings;
        modelDirectory = string.IsNullOrWhiteSpace(settings.ModelDirectory) ? null : Path.GetFullPath(settings.ModelDirectory);
    }

    /// <inheritdoc />
    public string ProviderId => LocalId;

    /// <inheritdoc />
    public string Name => "AIKernel Local Embedding";

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
            available ? "The pinned embedding model is loaded." : "The embedding model is not loaded.", DateTime.UtcNow, 0));
    }

    /// <inheritdoc />
    public IProviderCapabilities GetCapabilities() => new Capabilities(Available);

    /// <inheritdoc />
    public ProviderCapabilityDescriptor GetCapabilityDescriptor()
    {
        bool available = Available;
        const int attentionHeads = 12;
        long attentionBytes = (long)settings.MaxBatchSize * settings.MaxTokens * settings.MaxTokens * attentionHeads * sizeof(float);
        long tensorBytes = (long)settings.MaxBatchSize * settings.MaxTokens * E5Model.Dimensions * sizeof(float);
        return new()
        {
            SchemaVersion = ProviderPlacementAbi.SchemaVersion,
            ProviderId = ProviderId,
            Kind = ProviderKind.Embedding,
            ModelIds = available ? ModelIds : [],
            InputModalities = [ProviderModality.Text],
            ExecutionTargets = [ExecutionTarget.Cpu],
            Modes = ExecutionMode.Single | ExecutionMode.Batch,
            Precisions = ["FP32"],
            Memory = new() { WorkingSetBytes = 3L * E5Model.WeightsBytes + 4 * attentionBytes + 8 * tensorBytes, Basis = "estimated" },
            Limits = new()
            {
                MaxItemsPerRequest = settings.MaxBatchSize,
                MaxItemLength = settings.MaxTokens,
                MaxItemLengthUnit = "tokens",
                MaxRequestBytes = settings.MaxRequestBytes
            },
            Extension = new EmbeddingCapabilityExtension
            {
                SupportedOutputKinds = EmbeddingOutputKind.Dense,
                DefaultOutputKinds = EmbeddingOutputKind.Dense,
                DefaultDimensions = E5Model.Dimensions,
                SupportsMatryoshka = false,
                SupportsNormalizeOption = true,
                DefaultNormalize = settings.DefaultNormalize,
                SupportedPurposes = [EmbeddingPurpose.Query, EmbeddingPurpose.Document],
                DefaultPurpose = settings.DefaultPurpose
            },
            Metadata = available ? Provenance : new ReadOnlyDictionary<string, string>(new Dictionary<string, string>())
        };
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (model is not null || modelDirectory is null)
                return;
            try
            {
                var loaded = await Task.Run(() => new E5Model(modelDirectory, settings.CpuThreads)).ConfigureAwait(false);
                Volatile.Write(ref model, loaded);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or OnnxRuntimeException or FormatException)
            {
                throw E5Model.Error(ProviderErrorCodes.ExecutionFailed, "The pinned embedding files could not be loaded or verified.");
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<EmbeddingResult> GenerateEmbeddingsAsync(EmbeddingRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        if (request.ModelId is not null && request.ModelId != E5Model.ModelId)
            throw NotRegistered();

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var loaded = model ?? throw NotRegistered();
            var texts = Validate(request, cancellationToken);
            var purpose = request.Purpose ?? settings.DefaultPurpose;
            var normalize = request.Normalize ?? settings.DefaultNormalize;
            return await Task.Run(() =>
            {
                var batch = new long[texts.Length][];
                for (var index = 0; index < texts.Length; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    batch[index] = loaded.Encode(texts[index], purpose, settings.MaxTokens);
                }
                return new EmbeddingResult
                {
                    ProviderId = ProviderId,
                    ModelId = E5Model.ModelId,
                    Dimensions = E5Model.Dimensions,
                    Normalized = normalize,
                    Items = loaded.Run(batch, normalize, cancellationToken)
                };
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private string[] Validate(EmbeddingRequest request, CancellationToken ct)
    {
        if (request.Inputs is null || request.Inputs.Count == 0 ||
            request.OutputKinds == EmbeddingOutputKind.None ||
            request.Purpose is EmbeddingPurpose purpose && !Enum.IsDefined(purpose) ||
            request.Dimensions is <= 0 or > E5Model.Dimensions)
            throw E5Model.Error(ProviderErrorCodes.InvalidRequest, "The embedding request is invalid.");
        if (request.OutputKinds is not (null or EmbeddingOutputKind.Dense) ||
            request.Dimensions is < E5Model.Dimensions)
            throw E5Model.Error(ProviderErrorCodes.UnsupportedOption, "The requested embedding output or dimension is unsupported.");
        var count = request.Inputs.Count;
        if (count > settings.MaxBatchSize)
            throw E5Model.Error(ProviderErrorCodes.InputLimitExceeded, "The embedding batch exceeds the configured item limit.");

        var texts = new string[count];
        long byteCount = 0;
        for (var index = 0; index < count; index++)
        {
            ct.ThrowIfCancellationRequested();
            var input = request.Inputs[index];
            if (input is null || input is EmbeddingInput.Text { Value: null })
                throw E5Model.Error(ProviderErrorCodes.InvalidRequest, "An embedding input is missing.");
            if (input is not EmbeddingInput.Text text)
                throw E5Model.Error(ProviderErrorCodes.UnsupportedModality, "The registered model accepts text inputs.");
            if (string.IsNullOrWhiteSpace(text.Value))
                throw E5Model.Error(ProviderErrorCodes.InvalidRequest, "Embedding input text is empty.");
            if (settings.MaxRequestBytes is long limit && (byteCount += Encoding.UTF8.GetByteCount(text.Value)) > limit)
                throw E5Model.Error(ProviderErrorCodes.InputLimitExceeded, "The embedding inputs exceed the configured byte limit.");
            texts[index] = text.Value;
        }
        return texts;
    }

    /// <inheritdoc />
    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var result = await GenerateEmbeddingsAsync(new() { Inputs = [new EmbeddingInput.Text(text)] }, cancellationToken).ConfigureAwait(false);
        return result.Items[0].Dense!;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(texts);
        if (texts.Count > settings.MaxBatchSize)
            throw E5Model.Error(ProviderErrorCodes.InputLimitExceeded, "The embedding batch exceeds the configured item limit.");
        var result = await GenerateEmbeddingsAsync(new()
        {
            Inputs = texts.Select(text => new EmbeddingInput.Text(text)).ToArray()
        }, cancellationToken).ConfigureAwait(false);
        return Array.AsReadOnly(result.Items.Select(item => item.Dense!).ToArray());
    }

    /// <inheritdoc />
    public int GetDimension() => Available ? E5Model.Dimensions : throw NotRegistered();

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
        E5Model.Error(ProviderErrorCodes.ModelNotRegistered, "The requested embedding model is not registered.");

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
        public bool SupportsDataType(string dataType) => available && DataTypes.Contains(dataType, StringComparer.OrdinalIgnoreCase);
        public bool SupportsQuantization(string quantizationLevel) =>
            available && StringComparer.OrdinalIgnoreCase.Equals(quantizationLevel, "FP32");
        public bool SupportsQueryAugmentation => false;
        public bool SupportsQueryDecomposition => false;
        public bool SupportsQueryRouting => false;
        public int MaxQueryParts => 0;
        public IReadOnlyList<string> SupportedQueryProcessingOperations => [];
        public bool SupportsQueryProcessingOperation(string operation) => false;
        public bool SupportsEmbedding => available;
        public int? EmbeddingDimensions => available ? E5Model.Dimensions : null;
        public IReadOnlyList<string> SupportedEmbeddingModels => available ? ModelIds : [];
    }
}
