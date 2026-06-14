# Provider Catalog

[English](index.md)

この catalog は、AIKernel.Providers 0.1.1 公開に含まれる Provider package
を説明します。

Provider project は `src/` 配下で category ごとに整理します。

- `Llm` - LLM / model hosting 系 Provider。
- `Chat` - chat domain の storage / history 系 Provider。
- `Compute` - native / accelerator compute 系 Provider。
- `Pipeline` - semantic pipeline compiler / pipeline orchestration 系 Provider。
- `Standard` - OS 標準 driver Provider。

## Inclusion Rule Summary

外部依存が runtime-configurable、manifest-driven、descriptor-driven、
endpoint-driven、または pure managed multi-platform .NET dependency として表現できる
Provider は AIKernel.Providers に残せます。

ビルド時に native runtime、OS SDK、browser/WASM runtime、vendor SDK、scenario
runtime、または別 top-level AIKernel implementation repository へ固定依存する
Provider は dedicated package / repository に分離します。

ここに置ける例は `LocalLlmProvider`、generic OpenAI-compatible HTTP provider、
`ChatHistoryProvider`、`DynamicPipelineCompilerProvider`、`CudaComputeProvider`
descriptor / invoker boundary です。generic Providers に置けない例は
`NAudioProvider`、`SdlAudioProvider`、`WebAudioProvider`、`WasmAudioProvider`、
WebGPU provider、`WindowsAIProvider`、CUDA native implementation、OpenAI / Azure
SDK-specific provider です。

[Provider development guidelines](../guidelines/provider-development-guidelines-ja.md) を参照してください。

## ChatOpenAIProvider

`ChatOpenAIProvider` は、公式 OpenAI 互換外部 Provider です。

以下を公開します。

- `openai.chat`
- `chat.completion`
- `embedding`
- `moderation`

この Provider は endpoint、model、API key、timeout、OpenAI-compatible client
boundary の設定を所有します。AIKernel.Tools から分離されているため、Tools は
instrumentation 専用に保たれます。

Manifest:

- `src/Chat/ChatOpenAIProvider/openai.provider.json`

## ChatHistoryProvider

`ChatHistoryProvider` は deterministic chat history record を Provider capability
として公開します。旧 RomStorage capability-provider role から抽出されました。

以下を公開します。

- `chat-history`
- deterministic record enumeration
- role-based record filtering
- latest-record lookup

Manifest:

- `src/Chat/ChatHistoryProvider/chat-history.provider.json`

## CudaComputeProvider

`CudaComputeProvider` は native CUDA compute module を AIKernel Provider
capability として抽象化します。CUDA device profile、entry point、loader
manifest、native artifact hash metadata を記述します。

CUDA 13 module などの native CUDA 実装を扱う Provider-level boundary です。

Manifest:

- `src/Compute/CudaComputeProvider/cuda.provider.json`

## DynamicPipelineCompilerProvider

`DynamicPipelineCompilerProvider` は dynamic semantic pipeline compiler を
Provider capability として公開します。

以下のような CLI install / loading flow を想定しています。

```bash
aik install provider dynamic-pipeline
```

host が pipeline compiler compatibility を検証できるように、DSL schema version
と schema URI metadata を記録します。

Manifest:

- `src/Pipeline/DynamicPipelineCompilerProvider/dynamic-pipeline.provider.json`

## AIKernel.Providers.Standard

`AIKernel.Providers.Standard` は、AIKernel host 向けの OS 標準 driver Provider
package です。Browser / WASM 固有の実装はこの package の外に置き、host 側の
OS service に集中します。

含まれる Provider:

- `CpuComputeProvider`
- `MemoryFileSystemProvider`
- `PhysicalFileSystemProvider`
- `ZipFileSystemProvider`
- `ConsoleLoggingProvider`
- `FileLoggingProvider`
- `ProcessSupervisorProvider`
- `EventBusProvider`
- `NetworkProvider`
- `SchedulerProvider`
- `ProfilerProvider`

Manifest:

- `src/Standard/AIKernel.Providers.Standard/standard.provider.json`

## LocalLlmProvider

`LocalLlmProvider` は Ollama、llama.cpp、vLLM などの local LLM runtime を
AIKernel Provider boundary から公開します。

local runtime 名、runtime URI、任意の artifact hash metadata を記録します。
local inference provider を Core と Tools の外に置きながら、AIKernel capability
registration に参加できるようにします。

Manifest:

- `src/Llm/LocalLlmProvider/local-llm.provider.json`

## AIKernel.Providers.MicrosoftAI

`AIKernel.Providers.MicrosoftAI` は、Microsoft.Extensions.AI based model
execution を AIKernel Provider implementation として wrap します。

この Provider は 0.1.1 release で AIKernel.Core から AIKernel.Providers 管理へ
移管されました。Core は contract を定義し続け、この repository が
implementation、package metadata、test、documentation、Python wrapper 参照資料を
所有します。

package には以下が含まれます。

- `OpenAICompatibleProvider`
- `OpenAICompatibleProviderOptions`
- `OpenAICompatibleProviderCapabilities`
- `OpenAICompatibleResponseMapper`
- OpenAI-compatible provider hosting 向け dependency-injection extension

現 release では、この Provider は standalone manifest ではなく managed package
と DI extension surface として利用されます。

## Fail-Closed Implementation Rules

Provider implementation は public contract を安定させたまま、内部では monadic
composition を使用します。

- capability invoker は unsupported operation を `Option<T>` と structured
  `CapabilityInvocationResult` error として返す
- standard driver は missing process、event subscription、file-system entry、
  provider lookup に `Option<T>` を使う
- host I/O / network execution は `Result<T>` / `Try.RunAsync` 境界の内側に置く
- provider health / metadata selection の純粋な二分岐は `Either<L,R>` を使う

public contract が要求する nullable DTO field は package boundary に残します。
ただし内部 decision は `Option<T>` で表現してから DTO へ射影します。
