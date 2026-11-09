# AIKernel.Providers Development Guidelines

[日本語](provider-development-guidelines-ja.md)

AIKernel.Providers contains provider substrate and runtime-configurable providers.
A provider may stay in this repository when its external dependencies are expressed
through configuration, manifests, descriptors, endpoints, or pure managed
multi-platform .NET libraries.

Providers that require build-time fixed native runtimes, OS SDKs, browser/WASM
runtimes, vendor SDKs, scenario state, or another top-level AIKernel implementation
repository must live in a dedicated package or repository.

AIKernel.Providers は provider substrate と runtime-configurable provider を保持します。
外部依存が設定、manifest、descriptor、endpoint、または pure managed multi-platform .NET library
として抽象化できる場合、その Provider はこの repository に残せます。

一方、ビルド時に native runtime、OS SDK、browser/WASM runtime、vendor SDK、
scenario state、または別の top-level AIKernel 実装 repository へ固定依存する Provider は、
専用 package / repository に分離しなければなりません。

## Provider Inclusion Rule

A provider may live in AIKernel.Providers if its dependencies are runtime-configurable,
manifest-driven, or pure managed multi-platform .NET dependencies.

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

LLM / SLM providers may propose candidates, evidence, diagnostics, and capability
material. AIKernel Core / Governance decides. Provider output must not become
execution authority by itself.

Council semantic providers must keep semantic dimensions separate from Gate
input. `ProviderSemanticResult.Dimensions` has a stable minimum key set so
Control can normalize provider output deterministically, but those values are
not copied into `GateInput`.

| Council | Minimum dimension keys |
| --- | --- |
| Logos | `logos.logical_consistency`, `logos.evidence_grounding`, `logos.causal_coherence` |
| Ethos | `ethos.safety_alignment`, `ethos.permission_alignment`, `ethos.reversibility` |
| Pathos | `pathos.context_alignment`, `pathos.user_intent_alignment`, `pathos.impact_alignment` |

Providers may emit additional dimension keys, evidence, diagnostics, and
metadata. Additional keys remain semantic material unless a Control adapter
explicitly consumes them during normalization.

Manifest fields that affect routing, registry construction, and dependency
boundary checks must be strongly typed. Provider-specific and vendor-specific
fields must stay in loose metadata or raw extension JSON so future manifest
versions remain non-breaking.

Manifest loader and validator services should be consumed through
`IProviderManifestLoader` and `IProviderManifestValidator` when hosts need to
replace parsing or validation behavior. Concrete providers should not bypass
the descriptor boundary by reading vendor SDK configuration directly.

Manifest validation failures should expose both `ProviderDiagnostic` entries
and `ProviderManifestValidationError` DTOs. Diagnostics are optimized for
operator visibility, while validation errors are stable carrier records for
strict host policy, CI checks, and future schema migration.

Control integration must treat Provider output as semantic material only.
`ProviderVoteAdapter` is responsible for converting `ProviderSemanticResult`
into Control-side council votes. Providers may return `ProposedVoteValue`,
`Status`, evidence, dimensions, diagnostics, confidence, and risk score, but
they must not create `GateInput` or emit Gate decisions. Confidence and risk
score remain diagnostics and are not copied into `GateInput`.

The following provider substrate may live in AIKernel.Providers:

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
- provider with pure managed multi-platform .NET dependencies

Examples that may live in AIKernel.Providers:

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

The following must not live in generic AIKernel.Providers packages:

- build-time fixed native runtime dependency
- build-time fixed OS SDK dependency
- build-time fixed vendor SDK dependency
- browser / JSInterop / WASM runtime fixed dependency
- scenario-specific runtime dependency
- another top-level AIKernel implementation repository dependency

Examples that must move to dedicated packages or repositories:

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

A provider in AIKernel.Providers must have a dependency closure that stays within:

- AIKernel.NET contracts
- AIKernel.Core, only when necessary
- AIKernel.Providers common packages
- pure managed multi-platform .NET libraries
- runtime configuration, manifests, descriptors, endpoints, or backend names

It must not require:

- native runtime or native library at build time
- OS SDK or platform-specific API at build time
- browser / JSInterop / WASM runtime at build time
- another top-level AIKernel implementation repository
- scenario-specific runtime state

The dependency closure must be readable from project references, package
references, manifests, and documentation. If a reviewer cannot tell whether a
provider is generic substrate or a fixed runtime integration, it should be split
or documented before implementation continues.

## Runtime-configurable Dependencies

Runtime-configurable providers describe external backends by configuration,
manifest fields, endpoint names, runtime names, model names, capability
descriptors, or backend descriptors.

`LocalLlmProvider` may stay in AIKernel.Providers because it treats external
runtimes as runtime configuration. It does not compile against Ollama,
llama.cpp, vLLM, LM Studio, TGI, exllama, TensorRT-LLM, or ONNX Runtime
implementations directly.

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

`CudaComputeProvider` may stay in AIKernel.Providers only if CUDA native
modules, CUDA runtime, ABI loader, device profile implementation, native memory
operations, and kernels are fully contained in a dedicated backend package such
as AIKernel.Cuda13.0.

AIKernel.Providers may contain the descriptor, manifest, invoker boundary, and
backend selection metadata, but not the native CUDA implementation.

```yaml
backend: "cuda13.0"
nativeModuleRef: "aikernel-cuda://cuda13.0/modules/vector_add"
deviceProfile: "auto"
```

## Pure .NET Multi-platform Dependencies

A pure managed .NET dependency may be used inside AIKernel.Providers if it is
multi-platform and does not introduce native assets, RID-specific binaries,
OS SDK dependencies, browser/WASM runtime dependencies, P/Invoke requirements,
or another top-level AIKernel implementation repository dependency.

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

Build-time fixed dependencies bind the generic provider package to a concrete
runtime, SDK, ABI, platform API, browser API, or scenario runtime before the
host has a chance to choose a backend. Those dependencies do not belong in the
generic AIKernel.Providers substrate.

Examples of build-time fixed dependencies:

- native runtime packages
- OS SDKs or platform-specific APIs
- vendor SDKs such as OpenAI SDK-specific or Azure SDK-specific implementations
- browser / JSInterop / WASM runtime packages
- scenario-specific runtime state
- another top-level AIKernel implementation repository

## Dedicated Provider Packages

Use a dedicated package or repository when a provider needs a fixed runtime
implementation. The generic AIKernel.Providers repository may define the
descriptor, registry, router, manifest schema, base class, and invoker boundary;
the dedicated package owns the runtime implementation.

Examples that must be dedicated packages or repositories:

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

In the v0.1.3 canonical series, validate `aikernel-providers` with
`0.1.3.dev{buildNumber}` wheels until stable publication is explicitly opened.
The stable default Python install target is:

```bash
pip install aikernel-providers
```

The default Python wrapper may include:

- provider substrate
- manifest schema
- registry / router
- provider base classes
- common envelopes
- runtime-configurable providers
- pure managed multi-platform dependencies

The default Python wrapper must not include:

- NAudio
- SDL
- WebAudio
- Wasm
- CUDA native runtime
- Windows SDK
- OpenAI SDK-specific implementation
- Azure SDK-specific implementation

Dedicated install examples:

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

| Component | Can live in AIKernel.Providers | Reason |
| --- | ---: | --- |
| ProviderManifestSchema | OK | Abstract schema |
| ProviderRouter / Registry | OK | Abstract resolution |
| ProviderEvaluationEnvelope | OK | Common envelope |
| CouncilSemanticEvaluationProviderBase | OK | Common base |
| AudioPlayBase / AudioRecBase | OK | Common base only |
| ComputeProviderBase | OK | Common base only |
| LocalLlmProvider | OK | Runtime / endpoint abstracted by configuration |
| OpenAI-compatible generic HTTP provider | OK | Endpoint / model / key abstracted by configuration |
| Microsoft.Extensions.AI abstraction provider | OK with conditions | OK if pure managed / multi-platform abstraction |
| ChatHistoryProvider | OK | Deterministic local records |
| DynamicPipelineCompilerProvider | OK | DSL / schema configuration |
| CudaComputeProvider descriptor / invoker | OK | Backend descriptor only |
| CUDA native implementation | NG | Native runtime dependency |
| NAudioProvider | NG | Native / OS audio stack dependency |
| SDL Provider | NG | SDL native binding dependency |
| WebAudioProvider | NG | JSInterop / browser runtime dependency |
| WasmAudioProvider | NG | AIKernel.Wasm runtime dependency |
| WindowsAIProvider | NG | WinRT / Windows SDK dependency |
| OpenAI SDK-specific provider | NG | Vendor SDK fixed dependency |
| Azure SDK-specific provider | NG | Azure SDK fixed dependency |

## CUDA Clarification

CudaComputeProvider is allowed in AIKernel.Providers only as an abstract,
configuration-driven compute provider.

It may contain:

- `ICudaComputeProvider`
- `CudaComputeInvoker`
- `CudaComputeCapabilityDescriptor`
- `CudaBackendDescriptor`
- `NativeModuleDescriptor`
- provider manifest

It must not contain:

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

CI and review checks should fail or require explicit justification when a
generic provider package introduces a build-time fixed dependency. The exception
path is not to weaken this repository boundary; it is to create a dedicated
provider package or repository.
