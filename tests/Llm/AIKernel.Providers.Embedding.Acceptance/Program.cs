using System.Diagnostics;
using System.Text.Json;
using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Capabilities;
using AIKernel.Dtos.Providers;
using AIKernel.Dtos.Providers.Embedding;
using AIKernel.Kernel.Providers;
using AIKernel.Providers.Embedding;
using AIKernel.Providers.LocalLlm;

if (args.Length != 3)
    throw new ArgumentException("Usage: <pinned ONNX directory> <official reference JSON> <verification output JSON>");

var reference = JsonSerializer.Deserialize<ReferenceFixture>(File.ReadAllText(args[1]))!;
var unregisteredLegacy = await new LocalLlmInvoker().InvokeAsync(new CapabilityInvocationRequest(
    "acceptance", "local-llm", "embedding.local", new Dictionary<string, string>(),
    null, "sha256:replay", new Dictionary<string, string> { ["input"] = "private text" }));
Check(!unregisteredLegacy.Succeeded && unregisteredLegacy.OutputHash is null &&
      unregisteredLegacy.ErrorCode == ProviderErrorCodes.ModelNotRegistered &&
      !unregisteredLegacy.Metadata.ContainsKey("input"), "old package's unregistered invocation fails without input details");

var options = new LocalEmbeddingSettings { ModelDirectory = args[0], CpuThreads = 2 };
await using (var missing = new LocalEmbeddingProvider(new LocalEmbeddingSettings()))
{
    await missing.InitializeAsync();
    Check(missing.GetCapabilityDescriptor().ModelIds.Count == 0 && !missing.GetCapabilities().SupportsOperation("embedding.local"),
        "unregistered descriptor and operation");
    await Fails(ProviderErrorCodes.ModelNotRegistered, () => missing.EmbedAsync("missing"));
}

var startup = Stopwatch.StartNew();
await using var provider = new LocalEmbeddingProvider(options);
await provider.InitializeAsync();
startup.Stop();
Check(await provider.IsAvailableAsync(), "registered model is ready");
var descriptor = provider.GetCapabilityDescriptor();
var modelId = descriptor.ModelIds.Single();
var embedding = (EmbeddingCapabilityExtension)descriptor.Extension!;
Check(descriptor.ProviderId == "embedding.local" &&
      descriptor.ExecutionTargets.SequenceEqual([ExecutionTarget.Cpu]) &&
      descriptor.InputModalities.SequenceEqual([ProviderModality.Text]) &&
      descriptor.Limits.MaxItemsPerRequest == 16 && descriptor.Limits.MaxItemLength == 512, "published capability defaults");
Check(embedding.DefaultDimensions == 384 && !embedding.SupportsMatryoshka &&
      !embedding.Features.Contains("embedding.mrl") && embedding.DefaultPurpose == EmbeddingPurpose.Query &&
      embedding.SupportedPurposes.SequenceEqual([EmbeddingPurpose.Query, EmbeddingPurpose.Document]), "embedding-specific descriptor");
Check(provider.GetCapabilities().SupportsOperation("embedding.local") && provider.GetCapabilities().SupportsEmbedding &&
      provider.GetCapabilities().EmbeddingDimensions == provider.GetDimension() && provider.GetDimension() == 384,
    "original capability metadata");

var now = DateTimeOffset.UtcNow;
var node = new NodeCapabilityDescriptor
{
    SchemaVersion = ProviderPlacementAbi.SchemaVersion, NodeId = "acceptance-cpu",
    Cpu = new() { Architecture = "x64", LogicalCores = Environment.ProcessorCount },
    Memory = new() { RamTotalBytes = 7_000_000_000, RamAvailableBytes = 7_000_000_000 },
    MemoryTiers = [], Load = new() { ActiveJobs = 0, QueuedJobs = 0 }, ObservedAtUtc = now
};
var placement = await new ProviderPlacementScheduler().ResolvePlacementAsync(new()
{
    ProviderId = provider.ProviderId, ModelId = modelId, RequiredModalities = [ProviderModality.Text],
    RequiredMode = ExecutionMode.Batch, RequiredFeatures = ["embedding.dense"], WorkingSetBytes = 0
}, [node], new Dictionary<string, NodeProviderRegistrationSnapshot>
{
    [node.NodeId] = new() { Providers = [new() { Descriptor = descriptor }], ObservedAtUtc = now }
});
Check(placement.Selected?.Target == ExecutionTarget.Cpu, "ABI 00 accepts the actual CPU provider");

double maxError = 0, maxNormError = 0;
var timings = new List<double>();
foreach (var item in reference.Cases)
{
    var timer = Stopwatch.StartNew();
    var result = await provider.GenerateEmbeddingsAsync(new()
    {
        Inputs = item.Texts.Select(text => new EmbeddingInput.Text(text)).ToArray(),
        Purpose = Enum.Parse<EmbeddingPurpose>(item.Purpose), Normalize = item.Normalize
    });
    timings.Add(timer.Elapsed.TotalMilliseconds);
    Check(result.Items.Count == item.Texts.Length && result.Dimensions == 384 &&
          result.ModelId == modelId && result.Normalized == item.Normalize && result.SparseDimension is null, "result metadata");
    for (int index = 0; index < result.Items.Count; index++)
    {
        var vector = result.Items[index];
        Check(vector.Index == index && vector.Dense is { Length: 384 } && vector.Sparse is null, "result index and shape");
        var error = vector.Dense!.Zip(item.Vectors[index], (x, y) => Math.Abs((double)x - y)).Max();
        maxError = Math.Max(maxError, error);
        Console.WriteLine($"{item.Purpose} normalized={item.Normalize} input={index} FP32 max abs error={error:G6}");
        Check(error <= 1e-4, $"official FP32 reference at {item.Purpose}/{index}");
        var normError = Math.Abs(Norm(vector.Dense!) - 1);
        if (item.Normalize)
        {
            maxNormError = Math.Max(maxNormError, normError);
            Check(normError <= 1e-5, "unit-norm gate");
        }
        else
            Check(normError > 1e-2, "normalization disabled");
    }

    if (item is { Purpose: "Query", Normalize: true })
    {
        float[][] vectors = result.Items.Select(x => x.Dense!).ToArray();
        Check(Dot(vectors[0], vectors[1]) > Dot(vectors[0], vectors[2]) &&
              Dot(vectors[3], vectors[4]) > Dot(vectors[3], vectors[5]) &&
              Dot(vectors[0], vectors[3]) > Dot(vectors[0], vectors[5]), "ja, en and cross-language retrieval ranking");
    }
}

IEmbeddingProvider legacy = provider;
var texts = reference.Cases[0].Texts[..2];
var legacySingle = await legacy.EmbedAsync(texts[0]);
var updatedSingle = await provider.GenerateEmbeddingsAsync(Request(texts[0]));
Check(legacySingle.SequenceEqual(updatedSingle.Items[0].Dense!), "original single API bit equality");
var legacyBatch = await legacy.EmbedBatchAsync(texts);
var updatedBatch = await provider.GenerateEmbeddingsAsync(Request(texts));
Check(legacyBatch.Zip(updatedBatch.Items).All(pair => pair.First.SequenceEqual(pair.Second.Dense!)), "original batch API bit equality");

await Fails(ProviderErrorCodes.ModelNotRegistered, () => provider.GenerateEmbeddingsAsync(Request("text") with { ModelId = "missing" }));
await Fails(ProviderErrorCodes.UnsupportedOption, () => provider.GenerateEmbeddingsAsync(Request("text") with { Dimensions = 128 }));
await Fails(ProviderErrorCodes.UnsupportedOption, () => provider.GenerateEmbeddingsAsync(Request("text") with { OutputKinds = EmbeddingOutputKind.Sparse }));
await Fails(ProviderErrorCodes.InvalidRequest, () => provider.GenerateEmbeddingsAsync(Request("text") with { Dimensions = 385 }));
await Fails(ProviderErrorCodes.InputLimitExceeded, () => provider.GenerateEmbeddingsAsync(Request(Enumerable.Repeat("text", 17).ToArray())));
await Fails(ProviderErrorCodes.InputLimitExceeded, () => provider.GenerateEmbeddingsAsync(Request("valid", reference.AtLimit + " image")));
foreach (var modality in new EmbeddingInput[]
{
    new EmbeddingInput.Image(new byte[] { 1, 2, 3 }, "image/png"),
    new EmbeddingInput.Audio(new byte[] { 1, 2, 3 }, "audio/wav"),
    new EmbeddingInput.Multimodal([new EmbeddingInput.Text("text"), new EmbeddingInput.Image(new byte[] { 1 }, "image/png")])
})
    await Fails(ProviderErrorCodes.UnsupportedModality, () => provider.GenerateEmbeddingsAsync(new()
    {
        Inputs = [new EmbeddingInput.Text("valid"), modality]
    }));
Check((await provider.EmbedAsync(reference.AtLimit)).Length == 384, "exact 512-token input succeeds");

using (var cancel = new CancellationTokenSource())
{
    var pending = provider.GenerateEmbeddingsAsync(Request(Enumerable.Repeat(reference.AtLimit, 16).ToArray()), cancel.Token);
    await Task.Delay(30);
    Check(!pending.IsCompleted, "the real large request is still executing");
    cancel.Cancel();
    try
    {
        await pending;
        throw new InvalidOperationException("Canceled inference unexpectedly returned results.");
    }
    catch (OperationCanceledException)
    {
        Check(pending.IsCanceled, "canceled batch produced no partial result");
    }
}
Check((await provider.EmbedAsync("CPU works after a canceled batch")).Length == 384, "reset cancellation state");
await provider.ShutdownAsync();
await Fails(ProviderErrorCodes.ModelNotRegistered, () => provider.EmbedAsync("stopped"));

await using var configured = new LocalEmbeddingProvider(options with
{
    MaxBatchSize = 2, MaxTokens = 32, MaxRequestBytes = 220, CpuThreads = 1,
    DefaultNormalize = false, DefaultPurpose = EmbeddingPurpose.Document
});
await configured.InitializeAsync();
var custom = configured.GetCapabilityDescriptor();
var customExtension = (EmbeddingCapabilityExtension)custom.Extension!;
Check(custom.Limits.MaxItemsPerRequest == 2 && custom.Limits.MaxItemLength == 32 && custom.Limits.MaxRequestBytes == 220 &&
      customExtension.DefaultPurpose == EmbeddingPurpose.Document && !customExtension.DefaultNormalize, "configuration and descriptor agree");
var configuredDefault = await configured.GenerateEmbeddingsAsync(Request(texts[0]));
var explicitDefaults = await configured.GenerateEmbeddingsAsync(Request(texts[0]) with { Purpose = EmbeddingPurpose.Document, Normalize = false });
Check(configuredDefault.Normalized == false &&
      configuredDefault.Items[0].Dense!.SequenceEqual(explicitDefaults.Items[0].Dense!), "configured defaults apply to execution");
var configuredReference = reference.Cases.Single(x => x is { Purpose: "Document", Normalize: false }).Vectors[0];
Check(configuredDefault.Items[0].Dense!.Zip(configuredReference, (x, y) => Math.Abs(x - y)).Max() <= 1e-4,
    "configured document default is compared to the official document vector");
await Fails(ProviderErrorCodes.InputLimitExceeded, () => configured.GenerateEmbeddingsAsync(Request("one", "two", "three")));
await Fails(ProviderErrorCodes.InputLimitExceeded, () => configured.EmbedAsync(string.Join(" ", Enumerable.Repeat("AI", 40))));
await Fails(ProviderErrorCodes.InputLimitExceeded, () => configured.EmbedAsync(new string('x', 221)));

using var current = Process.GetCurrentProcess();
current.Refresh();
var report = new
{
    Passed = true,
    BaseModelRevision = descriptor.Metadata["model_revision"],
    WeightsSha256 = descriptor.Metadata["weights_sha256"],
    TokenizerSha256 = descriptor.Metadata["tokenizer_sha256"],
    MaxReferenceComponentError = maxError,
    MaxNormError = maxNormError,
    CpuThreads = options.CpuThreads,
    InitializationMilliseconds = startup.Elapsed.TotalMilliseconds,
    ReferenceBatchMilliseconds = timings,
    PeakWorkingSetBytes = current.PeakWorkingSet64
};
string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(args[2], json);
Console.WriteLine(json);

static EmbeddingRequest Request(params string[] values) =>
    new() { Inputs = values.Select(value => new EmbeddingInput.Text(value)).ToArray() };

static double Norm(float[] vector) => Math.Sqrt(Dot(vector, vector));
static double Dot(float[] left, float[] right) => left.Zip(right, (a, b) => (double)a * b).Sum();

static void Check(bool condition, string detail)
{
    if (!condition) throw new InvalidOperationException("Acceptance check failed: " + detail);
}

static async Task Fails(string code, Func<Task> run)
{
    try { await run(); }
    catch (ProviderOperationException error) when (error.Error.Code == code)
    {
        Check(error.Error.Detail is null && error.InnerException is null, "diagnostics exclude request and internal details");
        return;
    }
    throw new InvalidOperationException("Expected provider error " + code);
}

internal sealed record ReferenceFixture(ReferenceBatch[] Cases, string AtLimit);
internal sealed record ReferenceBatch(string Purpose, bool Normalize, string[] Texts, float[][] Vectors);
