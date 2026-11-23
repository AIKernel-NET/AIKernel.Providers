using System.Collections.ObjectModel;
using System.Text;
using AIKernel.Abstractions.Models;
using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Core;
using AIKernel.Dtos.Providers;
using AIKernel.Dtos.Providers.Reranking;
using AIKernel.Dtos.Routing;
using Tokenizers.HuggingFace.Errors;
using OnnxRuntimeException = Microsoft.ML.OnnxRuntime.OnnxRuntimeException;

namespace AIKernel.Providers.Reranker;

/// <summary>[EN] CPU cross-encoder provider for the pinned mMARCO MiniLM model. [JA] 固定版mMARCO MiniLMを使うCPU再順位付けProvider。</summary>
public sealed class LocalRerankerProvider : IRerankerProvider, IAsyncDisposable
{
    private const string LocalId = "rerank.local";
    private const long ModelLoadBudgetBytes = 2_000_000_000;
    private static readonly IReadOnlyList<string> ModelIds = Array.AsReadOnly(new[] { MiniLmModel.ModelId });
    private static readonly IReadOnlyList<string> Operations = Array.AsReadOnly(new[] { LocalId });
    private static readonly IReadOnlyList<string> DataTypes = Array.AsReadOnly(new[] { "text" });
    private static readonly ScoreSemantics Semantics = new() { Kind = ScoreKind.Raw, HigherIsBetter = true };
    private static readonly IReadOnlyDictionary<string, string> Provenance =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["model_revision"] = MiniLmModel.Revision,
            ["weights_sha256"] = MiniLmModel.WeightsSha256,
            ["tokenizer_sha256"] = MiniLmModel.TokenizerSha256
        });
    private static readonly IReadOnlyDictionary<string, string> NoMetadata =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

    private readonly LocalRerankerSettings settings;
    private readonly string? modelDirectory;
    private readonly SemaphoreSlim gate = new(1, 1);
    private MiniLmModel? model;
    private bool disposed;

    /// <summary>[EN] Creates a provider with the supplied settings. [JA] 指定した設定でProviderを生成します。</summary>
    public LocalRerankerProvider(LocalRerankerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        this.settings = settings;
        modelDirectory = string.IsNullOrWhiteSpace(settings.ModelDirectory) ? null : Path.GetFullPath(settings.ModelDirectory);
    }

    /// <inheritdoc />
    public string ProviderId => LocalId;

    /// <inheritdoc />
    public string Name => "AIKernel Local Reranker";

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
            available ? "The pinned reranker is loaded." : "The reranker is not loaded.", DateTime.UtcNow, 0));
    }

    /// <inheritdoc />
    public IProviderCapabilities GetCapabilities() => new Capabilities(Available);

    /// <inheritdoc />
    public ProviderCapabilityDescriptor GetCapabilityDescriptor()
    {
        bool available = Available;
        long pairTokens = (long)settings.EffectiveBatchSize * settings.MaxPairTokens;
        long attentionBytes = pairTokens * settings.MaxPairTokens * MiniLmModel.AttentionHeads * sizeof(float);
        long hiddenBytes = pairTokens * MiniLmModel.HiddenSize * sizeof(float);
        long encodedBytes = (long)settings.MaxCandidates * settings.MaxPairTokens * sizeof(long);
        long inferenceBytes = 3L * MiniLmModel.WeightsBytes + 4 * attentionBytes + 8 * hiddenBytes + encodedBytes;
        return new()
        {
            SchemaVersion = ProviderPlacementAbi.SchemaVersion,
            ProviderId = ProviderId,
            Kind = ProviderKind.Reranker,
            ModelIds = available ? ModelIds : [],
            InputModalities = [ProviderModality.Text],
            ExecutionTargets = [ExecutionTarget.Cpu],
            Modes = ExecutionMode.Batch,
            Precisions = ["FP32"],
            Memory = new()
            {
                // Account for the verified model buffer and tokenizer during startup.
                WorkingSetBytes = Math.Max(ModelLoadBudgetBytes, inferenceBytes),
                Basis = "estimated"
            },
            Limits = new()
            {
                MaxItemsPerRequest = settings.MaxCandidates,
                MaxItemLength = settings.MaxCandidateTokens,
                MaxItemLengthUnit = "tokens",
                MaxRequestBytes = settings.MaxRequestBytes
            },
            Extension = new RerankerCapabilityExtension
            {
                ScoreSemantics = Semantics,
                MaxQueryLength = settings.MaxQueryTokens,
                MaxPairLength = settings.MaxPairTokens,
                ScoringMethod = "cross-encoder"
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
            if (model is not null || modelDirectory is null)
                return;
            try
            {
                var loaded = await Task.Run(() => new MiniLmModel(modelDirectory, settings.CpuThreads)).ConfigureAwait(false);
                Volatile.Write(ref model, loaded);
            }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException
                or OnnxRuntimeException or TokenizerLoadFileException)
            {
                throw MiniLmModel.Error(ProviderErrorCodes.ExecutionFailed, "The pinned reranker files could not be loaded or verified.");
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<RerankResult> RerankAsync(RerankRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request is null)
            throw MiniLmModel.Error(ProviderErrorCodes.InvalidRequest, "The reranking request is missing.");
        if (request.ModelId is not null && request.ModelId != MiniLmModel.ModelId)
            throw NotRegistered();

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var loaded = model ?? throw NotRegistered();
            var texts = Validate(request, cancellationToken);
            return await Task.Run(() => ScoreCandidates(loaded, request.Query, texts, request.TopK, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private string[] Validate(RerankRequest request, CancellationToken cancellationToken)
    {
        int count = request.Candidates?.Count ?? 0;
        if (string.IsNullOrWhiteSpace(request.Query) || count < 1 || request.TopK <= 0 || request.TopK > count)
            throw MiniLmModel.Error(ProviderErrorCodes.InvalidRequest, "The reranking query, candidates or TopK is invalid.");
        if (count > settings.MaxCandidates)
            throw MiniLmModel.Error(ProviderErrorCodes.InputLimitExceeded, "The request exceeds the configured candidate limit.");

        var texts = new string[count];
        long bytes = settings.MaxRequestBytes.HasValue ? Encoding.UTF8.GetByteCount(request.Query) : 0;
        if (bytes > settings.MaxRequestBytes)
            throw MiniLmModel.Error(ProviderErrorCodes.InputLimitExceeded, "The request exceeds the configured UTF-8 byte limit.");
        for (int index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? text = request.Candidates![index]?.Text;
            if (string.IsNullOrWhiteSpace(text))
                throw MiniLmModel.Error(ProviderErrorCodes.InvalidRequest, "A reranking candidate is empty.");
            if (settings.MaxRequestBytes is long limit && (bytes += Encoding.UTF8.GetByteCount(text)) > limit)
                throw MiniLmModel.Error(ProviderErrorCodes.InputLimitExceeded, "The request exceeds the configured UTF-8 byte limit.");
            texts[index] = text;
        }
        return texts;
    }

    private RerankResult ScoreCandidates(MiniLmModel loaded, string query, string[] candidates, int? topK,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var pairs = new long[candidates.Length][];
        try
        {
            var queryTokens = loaded.EncodeText(query, settings.MaxQueryTokens, "The query exceeds the configured token limit.");
            for (int index = 0; index < candidates.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var tokens = loaded.EncodeText(candidates[index], settings.MaxCandidateTokens,
                    "A candidate exceeds the configured token limit.");
                pairs[index] = MiniLmModel.CreatePair(queryTokens, tokens, settings.MaxPairTokens);
            }
        }
        catch (TokenizerEncodingException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw MiniLmModel.Error(ProviderErrorCodes.ExecutionFailed, "The reranking text could not be tokenized.");
        }

        var scores = new ScoredItem[pairs.Length];
        for (int offset = 0; offset < pairs.Length;)
        {
            int batchSize = Math.Min(settings.InferenceBatchSize, pairs.Length - offset);
            var logits = loaded.Score(pairs.AsSpan(offset, batchSize), cancellationToken);
            for (int index = 0; index < logits.Length; index++)
                scores[offset + index] = new ScoredItem { Index = offset + index, Score = new ProviderScore(logits[index]) };
            offset += batchSize;
        }

        var ranked = scores.OrderByDescending(item => item.Score.Value).ThenBy(item => item.Index);
        var items = ranked.Take(topK ?? scores.Length).ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        return new()
        {
            ProviderId = ProviderId,
            ModelId = MiniLmModel.ModelId,
            Scores = new() { Semantics = Semantics, Items = Array.AsReadOnly(items) }
        };
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
        MiniLmModel.Error(ProviderErrorCodes.ModelNotRegistered, "The requested reranker model is not registered.");

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
        public bool SupportsEmbedding => false;
        public int? EmbeddingDimensions => null;
        public IReadOnlyList<string> SupportedEmbeddingModels => [];
    }
}
