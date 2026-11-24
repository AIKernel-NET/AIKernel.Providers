# Local LLM Provider

`chat.local` は `AIKernel.Kernel 0.1.4` のCPU推論エンジンで応答を生成します。ホストがローカルの `Bonsai-1.7B-Q1_0.gguf` を指定します。最初の有効なリクエストで重みを開き、同じinvokerで再利用します。登録したモデルごとにinvokerを1つ保持し、ホスト終了時に破棄してください。

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
    new Dictionary<string, string> { ["prompt"] = "一文で挨拶してください。" },
    InputHash: null, ReplayLogHash: null, Metadata: new Dictionary<string, string>());

var result = await invoker.InvokeAsync(request);
Console.WriteLine(result.Succeeded ? result.Metadata["text"] : result.ErrorCode);
```

マニフェスト経由で引数なしのコンストラクターを使う場合は、インスタンス生成前にホストの環境変数 `AIKERNEL_LOCAL_LLM_MODEL_PATH` にモデルファイルのパスを設定してください。`SystemPrompt` はホスト設定です。利用者は `prompt` と、必要に応じてホスト上限以内の `max_new_tokens` を指定できます。省略時の生成上限にはホスト設定を使います。

成功時は `text`、トークン数、`finish_reason`、`cpu_backend` に加え、UTF-8の応答に対するSHA-256を `OutputHash` に返します。本文とハッシュは応答データとして扱い、ログへ保存する項目を決めてください。モデル未登録、ファイルの読み込み失敗や未対応形式、無効な引数、空の応答は失敗として返します。キャンセルは `OperationCanceledException` で通知します。

ローカル埋め込みには `AIKernel.Providers.Embedding` にモデルを登録し、`IEmbeddingRequestProvider` を使用します。このinvokerが `embedding.local` を受け取ると `provider.model_not_registered` を返します。
