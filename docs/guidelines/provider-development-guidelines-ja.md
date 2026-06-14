# AIKernel.Providers 開発ガイドライン

[English](provider-development-guidelines.md)

AIKernel.Providers は provider substrate と runtime-configurable provider を保持します。
外部依存が設定、manifest、descriptor、endpoint、または pure managed multi-platform .NET library
として抽象化できる場合、その Provider はこの repository に残せます。

一方、ビルド時に native runtime、OS SDK、browser/WASM runtime、vendor SDK、
scenario state、または別の top-level AIKernel 実装 repository へ固定依存する Provider は、
専用 package / repository に分離しなければなりません。

AIKernel.Providers contains provider substrate and runtime-configurable providers.
A provider may stay in this repository when its external dependencies are expressed
through configuration, manifests, descriptors, endpoints, or pure managed
multi-platform .NET libraries.

Providers that require build-time fixed native runtimes, OS SDKs, browser/WASM
runtimes, vendor SDKs, scenario state, or another top-level AIKernel implementation
repository must live in a dedicated package or repository.

## Provider Inclusion Rule

A provider may live in AIKernel.Providers if its dependencies are runtime-configurable,
manifest-driven, descriptor-driven, endpoint-driven, or pure managed multi-platform .NET dependencies.

A provider must move to a dedicated package or repository if it requires a build-time fixed dependency on a specific native runtime, OS SDK, browser/WASM runtime, vendor SDK, scenario runtime, or another top-level AIKernel implementation repository.

外部依存を持つ Provider でも、
その外部依存を設定・manifest・endpoint・runtime name・capability descriptor として抽象化でき、
ビルド時に特定実装へ固定依存しないなら、
AIKernel.Providers に含めてよい。

ただし、
ビルド時に特定 SDK / native library / runtime package / OS API /
AIKernel の別 top-level repository 実装へ固定依存するなら、
AIKernel.Providers には含めず、専用 package / repository に分離する。

LLM / SLM provider は候補、evidence、diagnostics、capability material を提案できます。
最終判断は AIKernel Core / Governance が行います。Provider output 自体を execution
authority にしてはいけません。

Council semantic Provider は semantic dimension と Gate input を分離します。
`ProviderSemanticResult.Dimensions` は Control が Provider output を deterministic
に normalize するための安定した最低 key set を持ちますが、これらの値は
`GateInput` へコピーしてはいけません。

| Council | 最低 dimension key |
| --- | --- |
| Logos | `logos.logical_consistency`, `logos.evidence_grounding`, `logos.causal_coherence` |
| Ethos | `ethos.safety_alignment`, `ethos.permission_alignment`, `ethos.reversibility` |
| Pathos | `pathos.context_alignment`, `pathos.user_intent_alignment`, `pathos.impact_alignment` |

Provider は追加の dimension key、evidence、diagnostics、metadata を出力できます。
追加 key は、Control adapter が normalization 時に明示的に消費しない限り、
semantic material のまま扱います。

routing、registry construction、dependency boundary check に影響する manifest
field は strong type として扱います。Provider-specific / vendor-specific な field は、
future manifest version が non-breaking であり続けるよう loose metadata または
raw extension JSON に逃がします。

host が parsing / validation behavior を差し替える必要がある場合、manifest loader /
validator service は `IProviderManifestLoader` と `IProviderManifestValidator`
経由で利用します。concrete Provider は vendor SDK configuration を直接読んで
descriptor boundary を迂回してはいけません。

Manifest validation failure は `ProviderDiagnostic` と
`ProviderManifestValidationError` DTO の両方で公開します。Diagnostics は operator
向けの可視性、validation error DTO は strict host policy、CI check、将来の schema
migration 向けの安定 carrier です。

Control integration では Provider output を semantic material としてのみ扱います。
`ProviderVoteAdapter` が `ProviderSemanticResult` を Control 側の council vote へ
変換します。Provider は `ProposedVoteValue`、`Status`、evidence、dimensions、
diagnostics、confidence、risk score を返せますが、`GateInput` を生成したり Gate
decision を emit してはいけません。Confidence / risk score は diagnostics に留まり、
`GateInput` へコピーしません。

AIKernel.Providers に置いてよい provider substrate:

- Provider interface
- Provider base class
- Provider common DTO / envelope
- Provider manifest schema
- Provider registry
- Provider router
- Provider resolution policy
- Provider capability descriptor
- deterministic fallback policy
- minimal stub provider
- runtime-configurable provider
- manifest-driven provider
- descriptor-driven provider
- endpoint-driven provider
- pure managed multi-platform .NET dependency を持つ provider

AIKernel.Providers に置いてよい例:

- `ICouncilSemanticEvaluationProvider`
- `CouncilSemanticEvaluationProviderBase`
- `AudioPlayBase` / `AudioRecBase`
- `ComputeProviderBase`
- `ProviderEvaluationEnvelope`
- `ProviderSemanticResult`
- `ProviderDiagnostic`
- `ProviderEvidenceRef`
- `ProviderManifestSchema`
- `ProviderRegistry`
- `ProviderRouter`
- `ProviderResolutionPolicy`
- `LocalLlmProvider`
- OpenAI-compatible generic HTTP provider
- Microsoft.Extensions.AI abstraction provider, if pure managed and multi-platform
- `DynamicPipelineCompilerProvider`
- `ChatHistoryProvider`
- `CudaComputeProvider` descriptor / invoker

generic AIKernel.Providers package に置いてはいけないもの:

- build-time fixed native runtime dependency
- build-time fixed OS SDK dependency
- build-time fixed vendor SDK dependency
- browser / JSInterop / WASM runtime fixed dependency
- scenario-specific runtime dependency
- another top-level AIKernel implementation repository dependency

dedicated package / repository に分離すべき例:

- `NAudioProvider`
- `SdlAudioProvider`
- `WebAudioProvider`
- `WasmAudioProvider`
- WebGPU provider
- `WindowsAIProvider`
- WinRT provider
- CUDA native module implementation
- OpenAI SDK-specific provider
- Azure SDK-specific provider

## Dependency Closure Rule

AIKernel.Providers 内の Provider の依存閉包は、次の範囲に収めます。

- AIKernel.NET contracts
- 必要な場合のみ AIKernel.Core
- AIKernel.Providers common packages
- pure managed multi-platform .NET libraries
- runtime configuration、manifest、descriptor、endpoint、backend name

次を build time に要求してはいけません。

- native runtime / native library
- OS SDK / platform-specific API
- browser / JSInterop / WASM runtime
- 別の top-level AIKernel implementation repository
- scenario-specific runtime state

依存閉包は project reference、package reference、manifest、documentation から読める必要があります。
Provider が generic substrate なのか fixed runtime integration なのか判断できない場合は、
実装を進める前に分離または明文化してください。

## Runtime-configurable Dependencies

Runtime-configurable provider は、外部 backend を configuration、manifest field、
endpoint name、runtime name、model name、capability descriptor、backend descriptor
として記述します。

`LocalLlmProvider` は外部 runtime を runtime configuration として扱うため、
AIKernel.Providers に残せます。Ollama、llama.cpp、vLLM、LM Studio、TGI、exllama、
TensorRT-LLM、ONNX Runtime 実装へ直接 compile-time 依存しません。

```yaml
runtime: "ollama"
endpoint: "http://localhost:11434"
model: "llama3"
```

```yaml
runtime: "vllm"
endpoint: "http://localhost:8000"
model: "Qwen/Qwen2.5"
```

`CudaComputeProvider` は CUDA native module、CUDA runtime、ABI loader、device
profile implementation、native memory operations、kernel が AIKernel.Cuda13.0
のような専用 backend package に完全に隔離される場合に限り、AIKernel.Providers
に残せます。

AIKernel.Providers には descriptor、manifest、invoker boundary、backend selection
metadata を置けます。ただし native CUDA implementation は置けません。

```yaml
backend: "cuda13.0"
nativeModuleRef: "aikernel-cuda://cuda13.0/modules/vector_add"
deviceProfile: "auto"
```

## Pure .NET Multi-platform Dependencies

pure managed .NET dependency は、multi-platform であり、native asset、RID-specific
binary、OS SDK dependency、browser/WASM runtime dependency、P/Invoke requirement、
別 top-level AIKernel implementation repository dependency を導入しない場合に限り
AIKernel.Providers 内で利用できます。

Allowed:

- generic HTTP client library
- JSON serializer / schema validator
- pure .NET retry / resilience library
- pure .NET manifest parser
- Microsoft.Extensions.AI abstraction, if used as a pure multi-platform abstraction
- pure .NET vector / math helper

Not allowed in generic AIKernel.Providers:

- NAudio
- SDL native binding
- Microsoft.JSInterop
- Windows SDK / WinRT
- CUDA runtime
- platform-specific native packages

## Build-time Fixed Dependencies

build-time fixed dependency は、host が backend を選ぶ前に generic provider package を
具体 runtime、SDK、ABI、platform API、browser API、scenario runtime へ固定します。
この種の依存は generic AIKernel.Providers substrate には置きません。

例:

- native runtime packages
- OS SDK / platform-specific APIs
- OpenAI SDK-specific / Azure SDK-specific implementation などの vendor SDK
- browser / JSInterop / WASM runtime packages
- scenario-specific runtime state
- 別 top-level AIKernel implementation repository

## Dedicated Provider Packages

fixed runtime implementation が必要な Provider は dedicated package / repository に
分離します。generic AIKernel.Providers repository は descriptor、registry、router、
manifest schema、base class、invoker boundary を定義できます。runtime implementation
は dedicated package が所有します。

専用 package / repository に分離すべき例:

- `NAudioProvider`
- `SdlAudioProvider`
- `WebAudioProvider`
- `WasmAudioProvider`
- WebGPU provider
- `WindowsAIProvider`
- WinRT provider
- CUDA native module implementation
- OpenAI SDK-specific provider
- Azure SDK-specific provider

## PyPI Distribution Boundary

Repository boundary is also a Python distribution boundary.

The aikernel-providers Python package wraps the C# packages from the
AIKernel.Providers repository. Therefore AIKernel.Providers must not include
build-time fixed native, OS-specific, browser/WASM-specific, vendor-SDK-specific,
or scenario-specific implementations that would pollute the default
aikernel-providers wheel.

0.1.1.1 line では AIKernel.Providers は NuGet-only であり、PyPI package を build /
publish しません。将来 Python release が明示的に予定された場合の default install
target は次の通りです。

```bash
pip install aikernel-providers
```

default Python wrapper に含めてよいもの:

- provider substrate
- manifest schema
- registry / router
- provider base classes
- common envelopes
- runtime-configurable providers
- pure managed multi-platform dependencies

含めないもの:

- NAudio
- SDL
- WebAudio
- Wasm
- CUDA native runtime
- Windows SDK
- OpenAI SDK-specific implementation
- Azure SDK-specific implementation

専用 install の例:

```bash
pip install aikernel-audio-naudio
pip install aikernel-audio-sdl
pip install aikernel-wasm
pip install aikernel-cuda13
pip install aikernel-windowsai
pip install aikernel-openai
pip install aikernel-microsoftai
```

## Examples

| Component | AIKernel.Providers に置けるか | 理由 |
| --- | ---: | --- |
| ProviderManifestSchema | OK | 抽象 schema |
| ProviderRouter / Registry | OK | 抽象 resolution |
| ProviderEvaluationEnvelope | OK | 共通 envelope |
| CouncilSemanticEvaluationProviderBase | OK | 共通 base |
| AudioPlayBase / AudioRecBase | OK | 共通 base のみ |
| ComputeProviderBase | OK | 共通 base のみ |
| LocalLlmProvider | OK | runtime / endpoint を設定で抽象化 |
| OpenAI-compatible generic HTTP provider | OK | endpoint / model / key を設定で抽象化 |
| Microsoft.Extensions.AI abstraction provider | OK 条件付き | pure managed / multi-platform abstraction なら OK |
| ChatHistoryProvider | OK | deterministic local records |
| DynamicPipelineCompilerProvider | OK | DSL / schema による設定 |
| CudaComputeProvider descriptor / invoker | OK | backend descriptor のみ |
| CUDA native implementation | NG | native runtime 依存 |
| NAudioProvider | NG | native / OS audio stack 依存 |
| SDL Provider | NG | SDL native binding 依存 |
| WebAudioProvider | NG | JSInterop / browser runtime 依存 |
| WasmAudioProvider | NG | AIKernel.Wasm runtime 依存 |
| WindowsAIProvider | NG | WinRT / Windows SDK 依存 |
| OpenAI SDK-specific provider | NG | vendor SDK 固定 |
| Azure SDK-specific provider | NG | Azure SDK 固定 |

## CUDA Clarification

CudaComputeProvider is allowed in AIKernel.Providers only as an abstract,
configuration-driven compute provider.

置いてよいもの:

- `ICudaComputeProvider`
- `CudaComputeInvoker`
- `CudaComputeCapabilityDescriptor`
- `CudaBackendDescriptor`
- `NativeModuleDescriptor`
- provider manifest

置いてはいけないもの:

- CUDA runtime
- CUDA native modules
- `libtorch_bridge`
- device profile implementation
- ABI loader implementation
- native memory operations
- GPU kernels

AIKernel.Providers:

```text
CUDA を知ってよい。
CUDA を実装してはいけない。
```

AIKernel.Cuda13.0:

```text
CUDA を実装する。
```

CudaComputeProvider must not reference AIKernel.Cuda13.0 or CUDA runtime
directly. It may reference only descriptor / manifest / invoker abstractions.

## CI Guard Rules

AIKernel.Providers substrate packages must not reference:

- NAudio
- SDL
- Microsoft.JSInterop
- Windows SDK / WinRT
- CUDA runtime
- AIKernel.Wasm
- AIKernel.Tools
- platform-specific implementation packages

Allowed:

- pure managed multi-platform .NET libraries
- generic HTTP clients
- JSON / schema / manifest libraries
- Microsoft.Extensions.AI abstraction if used in a platform-neutral way

CI / review では generic provider package に build-time fixed dependency が混入した場合、
失敗または明示的な分離判断を要求します。例外で repository boundary を弱めるのではなく、
dedicated provider package / repository を作成します。
