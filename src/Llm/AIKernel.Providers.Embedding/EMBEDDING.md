# AIKernel Local Embedding Provider

`AIKernel.Providers.Embedding` produces 384-dimension text vectors on the CPU from the official `intfloat/multilingual-e5-small` FP32 ONNX model. It supports the original AIKernel 0.1.4 text interfaces and the batch request interface in `AIKernel.Contracts.Embedding`. Inputs and inference stay on the host.

## Register the model

Download `onnx/model.onnx` and `onnx/tokenizer.json` from revision `614241f622f53c4eeff9890bdc4f31cfecc418b3` of the [model repository](https://huggingface.co/intfloat/multilingual-e5-small/tree/614241f622f53c4eeff9890bdc4f31cfecc418b3/onnx). Keep the two files together in the directory set by `ModelDirectory`.

| File | SHA-256 |
| --- | --- |
| `model.onnx` | `ca456c06b3a9505ddfd9131408916dd79290368331e7d76bb621f1cba6bc8665` |
| `tokenizer.json` | `0b44a9d7b51c3c62626640cda0e2c2f70fdacdc25bbbd68038369d14ebdf4c39` |

The model repository identifies the model license as MIT. The NuGet package ships the provider code; the model and tokenizer files are downloaded separately. The provider verifies the file lengths and checksums before using them.

Inference uses [ONNX Runtime](https://github.com/microsoft/onnxruntime). The [Hugging Face Tokenizers .NET binding](https://github.com/IgnaciodelaTorreArias/Tokenizers.HuggingFace.DotNet) applies the official tokenizer pipeline, including whitespace around special tokens.

## Configure the provider

Create `LocalEmbeddingSettings` directly or bind it from the host's configuration. Settings apply to one instance. Rebuild the instance to change its settings. For example, a host using .NET configuration can bind the `Embedding` section:

```json
{
  "Embedding": {
    "ModelDirectory": "models/multilingual-e5-small",
    "MaxBatchSize": 16,
    "MaxTokens": 512,
    "MaxRequestBytes": null,
    "CpuThreads": 4,
    "DefaultNormalize": true,
    "DefaultPurpose": "Query"
  }
}
```

- `MaxBatchSize` and `MaxTokens` default to 16 and 512. Tokens include the automatically added prefix and special tokens. The model context limits `MaxTokens` to 512. The capability descriptor reports the configured limits.
- `MaxRequestBytes` optionally caps the combined UTF-8 length of the input texts, before prefixes.
- `CpuThreads` defaults to the lower of four and the number of available processors. Set it to 0 to use ONNX Runtime's default.
- Per-request `Normalize` and `Purpose` override the configured defaults. Use `Query` for a search query and `Document` for an indexed passage. The provider supplies the model's prefix.
- Hosts using `Microsoft.Extensions.Configuration.Binder` can load these settings with `configuration.GetRequiredSection("Embedding").Get<LocalEmbeddingSettings>()`.

```csharp
using AIKernel.Dtos.Providers.Embedding;
using AIKernel.Providers.Embedding;

await using var provider = new LocalEmbeddingProvider(new LocalEmbeddingSettings
{
    ModelDirectory = "models/multilingual-e5-small"
});
await provider.InitializeAsync();

var result = await provider.GenerateEmbeddingsAsync(new EmbeddingRequest
{
    Inputs = [new EmbeddingInput.Text("画像をPNGで保存する")],
    Purpose = EmbeddingPurpose.Document
});
float[] vector = result.Items[0].Dense!;
```

The available model accepts text and returns dense FP32 vectors. Requests for reduced dimensions, sparse output and unsupported modalities fail with provider error codes. Oversized batches and texts also fail. Inference observes cancellation. An instance without an initialized model has an empty model list and reports `provider.model_not_registered` for embedding requests.
