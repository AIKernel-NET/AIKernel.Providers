# リリースノート

## 0.1.1

AIKernel.Providers の初版公開です。AIKernel.NET 向け公式拡張 Provider
ワークスペースとして公開します。

この release では Provider 固有 logic の repository boundary を確立します。
Core は runtime contract と OS-level Provider を保持し、Tools は instrumentation
を保持し、AIKernel.Providers は公式外部 Provider driver を所有します。

### 追加

- OpenAI 互換 ChatCompletion、Embedding、Moderation capability manifest 向けに
  `ChatOpenAIProvider` を追加しました。
- deterministic chat history record 向けに `ChatHistoryProvider` を追加しました。
- native CUDA compute module を AIKernel Provider として抽象化する
  `CudaComputeProvider` を追加しました。
- dynamic semantic pipeline compiler の install / invocation 向けに
  `DynamicPipelineCompilerProvider` を追加しました。
- Ollama、llama.cpp、vLLM などの local LLM runtime 向けに `LocalLlmProvider`
  を追加しました。
- Microsoft.Extensions.AI を利用した `AIKernel.Providers.MicrosoftAI` を追加しました。
  この Provider は AIKernel.Core から AIKernel.Providers 管理へ移管されました。
- 動的ロード可能な Provider 向けの provider manifest JSON file を追加しました。
- managed assembly と manifest file を同梱する Python wrapper package
  `aikernel-providers` を追加しました。

### 変更

- Provider 固有 capability logic を AIKernel.Tools から分離し、Tools を純粋な
  instrumentation layer として維持します。
- MicrosoftAI Provider の所有を AIKernel.Core から移し、Core が外部 Provider
  実装 package を管理しない構造にしました。
- Provider package は 0.1.1 公開 semantics に揃え、開発中 build では
  `0.1.1-dev1` を使用します。

### 検証

- Provider project は `net10.0` package として build できます。
- Provider test suite は contract descriptor、manifest、dependency direction、
  Python wrapper assembly discovery を検証します。
- 生成 XML documentation は public package reference surface に対して
  `[EN]` / `[JA]` の bilingual summary を含みます。
