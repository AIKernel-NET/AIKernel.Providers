# AIKernel local reranker

`LocalRerankerProvider` runs the mMARCO multilingual MiniLM cross-encoder on the CPU. It accepts a query and text candidates, then returns raw relevance logits and the original zero-based candidate indices. Use the indices to retrieve the source URL or excerpt from the original search result. Higher scores rank first, ties follow input order, and all candidates are scored before applying `TopK`.

## Model files

Download [onnx/model.onnx](https://huggingface.co/cross-encoder/mmarco-mMiniLMv2-L12-H384-v1/blob/1427fd652930e4ba29e8149678df786c240d8825/onnx/model.onnx) and [tokenizer.json](https://huggingface.co/cross-encoder/mmarco-mMiniLMv2-L12-H384-v1/blob/1427fd652930e4ba29e8149678df786c240d8825/tokenizer.json) from revision `1427fd652930e4ba29e8149678df786c240d8825`. Put both files in `ModelDirectory` as `model.onnx` and `tokenizer.json`. The provider verifies the SHA-256 of both files on initialization.

Model files are distributed separately under [Apache-2.0](https://huggingface.co/cross-encoder/mmarco-mMiniLMv2-L12-H384-v1). When redistributing model files, include the license text and follow the applicable attribution and modification notice requirements.

## Configuration and use

Create `LocalRerankerSettings` directly or bind the host's `Reranker` configuration section using `Microsoft.Extensions.Configuration.Binder`. Create a new provider instance to apply changes.

```json
{
  "Reranker": {
    "ModelDirectory": "models/mmarco-mMiniLMv2-L12-H384-v1",
    "MaxCandidates": 16,
    "MaxQueryTokens": 128,
    "MaxCandidateTokens": 380,
    "MaxPairTokens": 512,
    "InferenceBatchSize": 4,
    "CpuThreads": 4,
    "MaxRequestBytes": null
  }
}
```

Query and candidate token limits exclude the four special tokens introduced by pairing. The pair limit includes them and cannot exceed 512. `MaxRequestBytes` optionally limits the UTF-8 size of all input texts together, counting the query once. An exceeded limit fails the entire request.

`InferenceBatchSize` limits how many pairs are scored per ONNX invocation. Each provider instance runs one request at a time. `CpuThreads` defaults to four or the number of available logical processors, whichever is smaller. Use zero for ONNX Runtime's default.

```csharp
using AIKernel.Dtos.Providers.Reranking;
using AIKernel.Providers.Reranker;
using Microsoft.Extensions.Configuration;

var settings = configuration.GetRequiredSection("Reranker").Get<LocalRerankerSettings>()
    ?? throw new InvalidOperationException("Missing reranker configuration.");
await using var provider = new LocalRerankerProvider(settings);
await provider.InitializeAsync();

var result = await provider.RerankAsync(new RerankRequest
{
    Query = "日本の首都はどこですか？",
    Candidates = [new("京都はかつて日本の都でした。"), new("日本の首都は東京です。")],
    TopK = 1
}, cancellationToken);
var bestCandidateIndex = result.Scores.Items[0].Index;
```

Check `GetCapabilityDescriptor()` for model registration, effective limits, CPU precision and score semantics. Scores are model-specific and have no declared numeric range. An empty query or candidate list, or `TopK` outside the candidate count, fails with `provider.invalid_request`. Cancellation throws `OperationCanceledException` without returning partial results.
