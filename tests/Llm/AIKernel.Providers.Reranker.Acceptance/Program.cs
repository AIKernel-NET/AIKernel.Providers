using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Providers;
using AIKernel.Dtos.Providers.Reranking;
using AIKernel.Providers.Reranker;
using Microsoft.Extensions.Configuration;

if (args.Length != 2)
    throw new ArgumentException("Provide the model directory and the reference script's output JSON.");

const double tolerance = 1e-4;
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var reference = JsonSerializer.Deserialize<Dictionary<string, double[]>>(File.ReadAllText(args[1]), json)!;
var cases = JsonSerializer.Deserialize<RankingCase[]>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "cases.json")), json)!;
var defaults = new LocalRerankerSettings();
Require(defaults.MaxCandidates == 16 && defaults.MaxQueryTokens == 128 && defaults.MaxCandidateTokens == 380 &&
    defaults.MaxPairTokens == 512 && defaults.InferenceBatchSize == 4 &&
    defaults.CpuThreads == Math.Min(4, Environment.ProcessorCount), "Unexpected default settings.");

string appSettings = JsonSerializer.Serialize(new
{
    Reranker = new
    {
        ModelDirectory = Path.GetFullPath(args[0]),
        MaxCandidates = 16,
        MaxQueryTokens = 129,
        MaxCandidateTokens = 381,
        MaxPairTokens = 512,
        InferenceBatchSize = 4,
        CpuThreads = 2,
        MaxRequestBytes = 32_768
    }
});
using var stream = new MemoryStream(Encoding.UTF8.GetBytes(appSettings));
var configuration = new ConfigurationBuilder().AddJsonStream(stream).Build();
var settings = configuration.GetRequiredSection("Reranker").Get<LocalRerankerSettings>()!;
Require(settings.CpuThreads == 2 && settings.InferenceBatchSize == 4, "CPU and inference batch settings did not bind.");
using var process = Process.GetCurrentProcess();

await using var unregistered = new LocalRerankerProvider(new());
await unregistered.InitializeAsync();
Require(!await unregistered.IsAvailableAsync() && unregistered.GetCapabilityDescriptor().ModelIds.Count == 0, "An unregistered model was advertised.");

await using var localProvider = new LocalRerankerProvider(settings);
IRerankerProvider provider = localProvider;
var simple = Request(cases[0].Query, cases[0].Candidates);
await ExpectError(unregistered, simple, ProviderErrorCodes.ModelNotRegistered);
await provider.InitializeAsync();
long startupPeak = GetPeak();
Require(await provider.IsAvailableAsync(), "The model was not loaded.");
var descriptor = provider.GetCapabilityDescriptor();
var extension = (RerankerCapabilityExtension)descriptor.Extension!;
Require(descriptor.ProviderId == "rerank.local" && descriptor.Kind == ProviderKind.Reranker &&
    descriptor.Modes == ExecutionMode.Batch && descriptor.ExecutionTargets.SequenceEqual(new[] { ExecutionTarget.Cpu }) &&
    descriptor.Precisions.SequenceEqual(new[] { "FP32" }) && descriptor.ModelIds.Count == 1 &&
    descriptor.Limits.MaxItemsPerRequest == 16 && descriptor.Limits.MaxItemLength == 381 &&
    descriptor.Limits.MaxItemLengthUnit == "tokens" && descriptor.Limits.MaxRequestBytes == 32_768 &&
    extension.MaxQueryLength == 129 && extension.MaxPairLength == 512 &&
    extension.ScoreSemantics is { Kind: ScoreKind.Raw, HigherIsBetter: true, Calibrated: false, Range: { Min: null, Max: null } },
    "Runtime descriptor does not match the configured provider.");

var rankingResults = new List<object>();
double maxAbsoluteError = 0;
foreach (var test in cases)
{
    var request = Request(test.Query, test.Candidates) with { ModelId = descriptor.ModelIds[0] };
    var result = await provider.RerankAsync(request);
    var items = result.Scores.Items;
    var expected = reference[test.Name];
    var order = Enumerable.Range(0, expected.Length).OrderByDescending(i => expected[i]).ThenBy(i => i);
    Require(result.ProviderId == descriptor.ProviderId && result.ModelId == request.ModelId &&
        result.Scores.Semantics == extension.ScoreSemantics && items.Select(x => x.Index).SequenceEqual(order) &&
        items.All(item => item.Label is null && double.IsFinite(item.Score.Value)) && items[0].Index == test.BestIndex,
        $"Unexpected ranking or metadata for {test.Name}.");
    double maxError = items.Max(item => Math.Abs(item.Score.Value - expected[item.Index]));
    Require(maxError <= tolerance, $"Reference difference for {test.Name}: {maxError:G17}.");
    maxAbsoluteError = Math.Max(maxAbsoluteError, maxError);

    foreach (int k in new[] { 1, 2, items.Count })
    {
        var top = await provider.RerankAsync(request with { TopK = k });
        Require(top.Scores.Items.SequenceEqual(items.Take(k)), $"TopK differs from the full result for {test.Name}.");
    }
    rankingResults.Add(new
    {
        test.Name,
        order = items.Select(item => item.Index).ToArray(),
        actual = items.OrderBy(item => item.Index).Select(item => item.Score.Value).ToArray(),
        expected,
        maxError
    });
}

// An application retains its search records and resolves citations by the original indices.
var hits = cases[0].Candidates.Select((excerpt, index) =>
    new { Excerpt = excerpt, Source = new Uri($"https://example.test/doc/{index}") }).ToArray();
var withSources = await provider.RerankAsync(Request(cases[0].Query, hits.Select(hit => hit.Excerpt)) with { TopK = 1 });
Require(hits[withSources.Scores.Items.Single().Index].Source.AbsolutePath == "/doc/5", "Source index was lost.");

await ExpectError(provider, simple with { Query = " " }, ProviderErrorCodes.InvalidRequest);
await ExpectError(provider, simple with { Candidates = [] }, ProviderErrorCodes.InvalidRequest);
await ExpectError(provider, Request("query", ["candidate", "  "]), ProviderErrorCodes.InvalidRequest);
await ExpectError(provider, simple with { TopK = 0 }, ProviderErrorCodes.InvalidRequest);
await ExpectError(provider, simple with { TopK = simple.Candidates.Count + 1 }, ProviderErrorCodes.InvalidRequest);
await ExpectError(provider, simple with { ModelId = "missing" }, ProviderErrorCodes.ModelNotRegistered);
await ExpectError(provider, Request("query", Enumerable.Repeat("candidate", 17)), ProviderErrorCodes.InputLimitExceeded);
await ExpectError(provider, Request(Repeat("text ", 130), ["candidate"]), ProviderErrorCodes.InputLimitExceeded);
await ExpectError(provider, Request("query", ["candidate", Repeat("text ", 382)]) with { TopK = 1 },
    ProviderErrorCodes.InputLimitExceeded);
await ExpectError(provider, Request(Repeat("text ", 128), [Repeat("text ", 381)]), ProviderErrorCodes.InputLimitExceeded);
await ExpectError(provider, Request("界", [new string('界', 10_922)]), ProviderErrorCodes.InputLimitExceeded);

var boundary = Request(Repeat("text ", 128), Enumerable.Repeat(Repeat("text ", 380), 16));
var maximum = await provider.RerankAsync(boundary);
long inferencePeak = GetPeak();
Require(maximum.Scores.Items.Count == 16 &&
    maximum.Scores.Items.Select(item => item.Index).SequenceEqual(Enumerable.Range(0, 16)) &&
    maximum.Scores.Items.All(item => item.Score == maximum.Scores.Items[0].Score), "Exact ties were not in input order.");
double boundaryError = maximum.Scores.Items.Max(item => Math.Abs(item.Score.Value - reference["boundary"][item.Index % 4]));
Require(boundaryError <= tolerance, "Reference difference at 512 tokens.");
maxAbsoluteError = Math.Max(maxAbsoluteError, boundaryError);

using (var canceled = new CancellationTokenSource())
{
    canceled.Cancel();
    await ExpectCanceled(provider.RerankAsync(boundary, canceled.Token), canceled.Token);
}

using var duringRun = new CancellationTokenSource();
var elapsed = Stopwatch.StartNew();
var inFlight = provider.RerankAsync(boundary, duringRun.Token);
await Task.Delay(40);
Require(!inFlight.IsCompleted, "The maximum-size request completed before cancellation could be tested.");
double cancelRequestedMs = elapsed.Elapsed.TotalMilliseconds;
duringRun.Cancel();
await ExpectCanceled(inFlight, duringRun.Token);
double canceledMs = elapsed.Elapsed.TotalMilliseconds;
var recovered = await provider.RerankAsync(simple with { TopK = 1 });
Require(recovered.Scores.Items.Single().Index == cases[0].BestIndex, "The session did not recover after cancellation.");

long peakWorkingSet = GetPeak();
await provider.ShutdownAsync();
Require(!await provider.IsAvailableAsync() && provider.GetCapabilityDescriptor().ModelIds.Count == 0,
    "Shutdown did not withdraw model registration.");
await ExpectError(provider, simple, ProviderErrorCodes.ModelNotRegistered);

Console.WriteLine(JsonSerializer.Serialize(new
{
    os = RuntimeInformation.OSDescription,
    runtime = RuntimeInformation.FrameworkDescription,
    processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
    executionTarget = "CPU",
    settings.CpuThreads,
    settings.InferenceBatchSize,
    settings.MaxPairTokens,
    settings.MaxCandidates,
    startupPeakWorkingSetBytes = startupPeak,
    inferencePeakWorkingSetBytes = inferencePeak,
    peakWorkingSetBytes = peakWorkingSet,
    descriptor.Memory.WorkingSetBytes,
    rankingResults,
    boundaryError,
    maxAbsoluteError,
    tolerance,
    cancelRequestedMs,
    canceledMs,
    checks = new[] { "TopK", "stable ties", "source indices", "configured limits", "invalid inputs",
        "unregistered model", "pre-cancel", "in-flight cancel", "post-cancel recovery", "shutdown" }
}, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));

long GetPeak()
{
    process.Refresh();
    return process.PeakWorkingSet64;
}

static RerankRequest Request(string query, IEnumerable<string> candidates) =>
    new() { Query = query, Candidates = candidates.Select(text => new RerankCandidate(text)).ToArray() };

static string Repeat(string text, int count) => string.Concat(Enumerable.Repeat(text, count));

static async Task ExpectError(IRerankProvider provider, RerankRequest request, string errorCode)
{
    try
    {
        await provider.RerankAsync(request);
    }
    catch (ProviderOperationException error) when (error.Error.Code == errorCode)
    {
        return;
    }
    throw new InvalidOperationException($"Expected {errorCode}.");
}

static async Task ExpectCanceled(Task<RerankResult> task, CancellationToken token)
{
    try
    {
        await task;
    }
    catch (OperationCanceledException error) when (error.CancellationToken == token)
    {
        return;
    }
    throw new InvalidOperationException("Expected cancellation without partial results.");
}

static void Require(bool ok, string message)
{
    if (!ok)
        throw new InvalidOperationException(message);
}

internal sealed record RankingCase(string Name, string Query, string[] Candidates, int BestIndex);
