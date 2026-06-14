# Provider Catalog

[English](index.md)

この catalog は、AIKernel.Providers 0.1.1 公開に含まれる Provider package
を説明します。

Provider project は `src/` 配下で category ごとに整理します。

- `ProviderSubstrate` - manifest loading、validation、registry、deterministic routing substrate。
- `Council` - semantic material と diagnostics のみを出力する CTG council semantic Provider。
- `Audio` - playback / recording Provider 向けの backend-independent audio substrate。
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

## Provider Substrate

`AIKernel.Providers.Substrate` は、Provider manifest、registry、deterministic
routing のための shared pure managed substrate です。manifest loading、
strict / permissive validation、`ProviderRegistry`、`ProviderRouter`、
`MissingProviderResult`、`DuplicateProviderDiagnostic`、
`DeterministicFallbackPolicy`、`ProviderDiagnostic`、`ProviderEvidenceRef` を含みます。
manifest loading / validation は `IProviderManifestLoader` と
`IProviderManifestValidator` も公開するため、host は Provider manifest を変えずに
loader / validator を差し替えられます。

Manifest loading は permissive です。未知の top-level JSON と未知の CLI JSON は
raw extension JSON として保持します。Validation は明示的に選択します。host は
`ProviderManifestValidationOptions` により capability や schema version を必須化できます。
descriptor は `ProviderId`、`ManifestVersion`、`PackageId`、`Dependencies`、
`BackendDescriptors`、`Compatibility` などの strong field を保持し、未知の将来 JSON は
raw extension map に残します。

Metadata の merge order は deterministic です。provider-level `metadata` を最初に
適用し、`backendMetadata` が provider-level key を override し、`vendorMetadata` が
manifest 境界内でその両方を override します。CLI hint は provider-level value を継承し、
override hint は scalar field を置換し、list field を ordinal deterministic order で追加します。

Provider routing は安定した rank key を使います。provider priority を最初に評価し、
続いて availability、exact capability match、backend preference rank
（`runtimeHints.backend`、明示的 backend preference order、任意の local-over-remote
preference）、provider id lexical order、provider version、manifest path、
capability specificity、backend rank、backend name lexical order を使います。
Availability probe order も同じ key を使います。完全に同じ rank key の candidate は
`DuplicateProviderDiagnostic` として報告し、missing provider は
`MissingProviderResult` として報告します。

## Council Semantic Providers

`AIKernel.Providers.Council` は CTG orchestration 向けに Logos / Ethos /
Pathos の semantic provider surface を提供します。これらの Provider は
`ProviderSemanticResult` と `ProviderDiagnostic` material のみを出力します。
組み込みの最小 Provider は、host が semantic backend を bind するまで
`Unknown` vote envelope を返します。
`SemanticEvaluationStatus` は Control が material を vote に normalize する前に、
`Unknown`、`Evaluated`、`NotApplicable`、`Inconclusive`、`Failed`、`Timeout`、
`ProviderUnavailable` を区別します。

Council Provider は Control 側の normalization 向けに deterministic な
`Dimensions` key も出力します。これらは semantic material のみであり、
`GateInput` へコピーしてはいけません。

| Council | 最低 dimension key |
| --- | --- |
| Logos | `logos.logical_consistency`, `logos.evidence_grounding`, `logos.causal_coherence` |
| Ethos | `ethos.safety_alignment`, `ethos.permission_alignment`, `ethos.reversibility` |
| Pathos | `pathos.context_alignment`, `pathos.user_intent_alignment`, `pathos.impact_alignment` |

Provider は必要に応じて追加 dimension key を定義できます。Control adapter は、
明示的に消費する設定がない限り、未知の追加 key を無視します。

## Audio Substrate

`AIKernel.Providers.Audio` は backend-independent な audio request / result 型、
`AudioFormat`、`AudioFrame`、`AudioFormatValidator`、`AudioPlayBase`、
`AudioRecBase`、audio routing helper を含みます。NAudio、SDL、WebAudio、WASM、
OS SDK、platform API への依存はありません。
`AudioRecordFrame` は Provider-neutral な `FrameIndex`、`SampleOffset`、
stream-relative `Timestamp` metadata を保持します。

## Compute Substrate

`AIKernel.Providers.Compute` は `ComputeBufferRef` により backend-neutral な
tensor-like metadata を標準化します。dtype は `f32`、`f16`、`i32`、`u8`
などの安定した string 値を使い、shape / stride は `1,256,256` などの
comma-separated string で表します。backend-specific detail は metadata map に逃がします。
`ComputeEntryPointDescriptor`、`ComputeParameterDescriptor`、
`ComputeReturnDescriptor` は backend-specific public API 型を出さずに module /
function boundary を記述します。

`CudaComputeProvider` は descriptor-driven のままです。CUDA capability 名と
native module reference は知りますが、CUDA runtime package を参照または load
しません。認識済み operation は、dedicated backend が install / bind されるまで
`CUDA_BACKEND_NOT_BOUND` を返します。`ComputeAvailabilityReason` は provider missing、
backend not installed、native module missing、device unsupported、ABI mismatch、
unsupported operation を区別します。

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
