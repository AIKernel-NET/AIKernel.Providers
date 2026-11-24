# Local LLM Provider

`chat.local` generates a reply using the CPU inference engine in AIKernel.Kernel 0.1.4. The host supplies a local Bonsai 1.7B Q1_0 GGUF file. The provider opens the weights on the first valid request and reuses the model within that invoker. Use one invoker per registered model and dispose it at host shutdown.

```csharp
using AIKernel.Dtos.Capabilities;
using AIKernel.Providers.LocalLlm;

using var invoker = new LocalLlmInvoker(new LocalLlmSettings
{
    BonsaiModelPath = "/models/Bonsai-1.7B-Q1_0.gguf",
    CpuThreads = 4,
    ContextTokens = 1024,
    MaxPromptCharacters = 4096,
    MaxNewTokens = 128
});

var request = new CapabilityInvocationRequest(
    "example-1", "local-llm", "chat.local",
    new Dictionary<string, string> { ["prompt"] = "Write a one-sentence greeting." },
    InputHash: null, ReplayLogHash: null, Metadata: new Dictionary<string, string>());

var result = await invoker.InvokeAsync(request);
Console.WriteLine(result.Succeeded ? result.Metadata["text"] : result.ErrorCode);
```

For a manifest loader using the parameterless constructor, set `AIKERNEL_LOCAL_LLM_MODEL_PATH` in the host process before constructing the invoker. The model file is supplied by the host. `SystemPrompt` is host configuration. A request can supply `prompt` and optionally `max_new_tokens` within the host-configured cap. The cap is used when the argument is omitted.

The successful result includes `text`, token counts, `finish_reason`, `cpu_backend`, and a SHA-256 digest of the UTF-8 reply in `OutputHash`. Treat the text and digest as response data when deciding what to log. Missing models, unreadable or unsupported model files, invalid arguments, and empty replies return explicit failures. Cancellation raises `OperationCanceledException`.

For local embeddings, register an embedding model with `AIKernel.Providers.Embedding` and call its `IEmbeddingRequestProvider` contract. Requests for `embedding.local` to this invoker return `provider.model_not_registered`.
