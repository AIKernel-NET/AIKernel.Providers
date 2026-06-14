# Provider Catalog

[日本語](index-ja.md)

This catalog describes the provider packages included in the 0.1.1
AIKernel.Providers release.

Provider projects are grouped under `src/` by category:

- `ProviderSubstrate` - manifest loading, validation, registry, and deterministic routing substrate.
- `Council` - CTG council semantic providers that emit semantic material and diagnostics only.
- `Audio` - backend-independent audio substrate for playback and recording providers.
- `Llm` - LLM and model-hosting providers.
- `Chat` - chat-domain storage and history providers.
- `Compute` - native and accelerator compute providers.
- `Pipeline` - semantic pipeline compiler and pipeline orchestration providers.
- `Standard` - OS standard driver providers.

## Inclusion Rule Summary

Providers may stay in AIKernel.Providers when external dependencies are
runtime-configurable, manifest-driven, descriptor-driven, endpoint-driven, or
pure managed multi-platform .NET dependencies.

Providers must move to a dedicated package or repository when they require a
build-time fixed native runtime, OS SDK, browser/WASM runtime, vendor SDK,
scenario runtime, or another top-level AIKernel implementation repository.

Examples that belong here include `LocalLlmProvider`, generic
OpenAI-compatible HTTP providers, `ChatHistoryProvider`,
`DynamicPipelineCompilerProvider`, and `CudaComputeProvider` descriptor /
invoker boundaries. Examples that do not belong in generic Providers include
`NAudioProvider`, `SdlAudioProvider`, `WebAudioProvider`, `WasmAudioProvider`,
WebGPU providers, `WindowsAIProvider`, CUDA native implementations, and
SDK-specific OpenAI / Azure providers.

See [Provider development guidelines](../guidelines/provider-development-guidelines.md).

## Provider Substrate

`AIKernel.Providers.Substrate` is the shared pure managed substrate for provider
manifests, registries, and deterministic routing. It includes manifest loading,
strict/permissive validation, `ProviderRegistry`, `ProviderRouter`,
`MissingProviderResult`, `DuplicateProviderDiagnostic`,
`DeterministicFallbackPolicy`, `ProviderDiagnostic`, and `ProviderEvidenceRef`.
Manifest loading and validation expose `IProviderManifestLoader` and
`IProviderManifestValidator` so hosts can replace parsing or validation without
changing provider manifests.

Manifest loading is permissive: unknown top-level JSON and unknown CLI JSON are
preserved as raw extension JSON. Validation is explicit: hosts may require
capabilities or schema version through `ProviderManifestValidationOptions`.
The descriptor keeps strong fields such as `ProviderId`, `ManifestVersion`,
`PackageId`, `Dependencies`, `BackendDescriptors`, and `Compatibility`, while
unknown future JSON remains in raw extension maps.

Metadata merge order is deterministic. Provider-level `metadata` is applied
first, `backendMetadata` overrides provider-level keys, and `vendorMetadata`
overrides both inside the manifest boundary. CLI hints inherit provider-level
values; override hints replace scalar fields and append list fields using
ordinal deterministic ordering.

Provider routing uses a stable rank key. Provider priority is evaluated first,
then availability, exact capability match, backend preference rank
(`runtimeHints.backend`, explicit backend preference order, and optional
local-over-remote preference), provider id lexical order, provider version,
manifest path, capability specificity, backend rank, and backend name lexical
order. Availability probe order uses the same key. Candidates with the same
complete rank key are reported through `DuplicateProviderDiagnostic`; missing
providers are reported through `MissingProviderResult`.

## Council Semantic Providers

`AIKernel.Providers.Council` provides Logos, Ethos, and Pathos semantic provider
surfaces for CTG orchestration. These providers emit `ProviderSemanticResult`
and `ProviderDiagnostic` material only. The minimal built-in providers return
an `Unknown` vote envelope until a host binds a semantic backend.
`SemanticEvaluationStatus` distinguishes `Unknown`, `Evaluated`,
`NotApplicable`, `Inconclusive`, `Failed`, `Timeout`, and
`ProviderUnavailable` before Control normalizes material into votes.

Council providers also emit deterministic `Dimensions` keys for Control-side
normalization. These dimensions are semantic material only and must not be
copied into `GateInput`.

| Council | Minimum dimension keys |
| --- | --- |
| Logos | `logos.logical_consistency`, `logos.evidence_grounding`, `logos.causal_coherence` |
| Ethos | `ethos.safety_alignment`, `ethos.permission_alignment`, `ethos.reversibility` |
| Pathos | `pathos.context_alignment`, `pathos.user_intent_alignment`, `pathos.impact_alignment` |

Providers may add additional dimension keys when needed. Control adapters must
ignore unknown additional keys unless explicitly configured to consume them.

## Audio Substrate

`AIKernel.Providers.Audio` contains backend-independent audio request/result
types, `AudioFormat`, `AudioFrame`, `AudioFormatValidator`, `AudioPlayBase`,
`AudioRecBase`, and audio routing helpers. It has no NAudio, SDL, WebAudio,
WASM, OS SDK, or platform API dependency.
`AudioRecordFrame` carries provider-neutral `FrameIndex`, `SampleOffset`, and
stream-relative `Timestamp` metadata.

## Compute Substrate

`AIKernel.Providers.Compute` standardizes backend-neutral tensor-like metadata
through `ComputeBufferRef`. Dtypes use stable string values such as `f32`,
`f16`, `i32`, and `u8`; shapes and strides are comma-separated strings such as
`1,256,256`. Backend-specific details belong in the metadata map.
`ComputeEntryPointDescriptor`, `ComputeParameterDescriptor`, and
`ComputeReturnDescriptor` describe module/function boundaries without exposing
backend-specific public API types.

`CudaComputeProvider` remains descriptor-driven: it knows CUDA capability names
and native module references, but it does not reference or load CUDA runtime
packages. Recognized operations return `CUDA_BACKEND_NOT_BOUND` until a
dedicated backend is installed and bound. `ComputeAvailabilityReason` separates
provider missing, backend not installed, native module missing, device
unsupported, ABI mismatch, and unsupported operation states.

## ChatOpenAIProvider

`ChatOpenAIProvider` is the official OpenAI-compatible external provider.

It exposes:

- `openai.chat`
- `chat.completion`
- `embedding`
- `moderation`

The provider owns endpoint, model, API key, timeout, and OpenAI-compatible
client boundary settings. It is separated from AIKernel.Tools so Tools remains
instrumentation-only.

Manifest:

- `src/Chat/ChatOpenAIProvider/openai.provider.json`

## ChatHistoryProvider

`ChatHistoryProvider` exposes deterministic chat history records as a provider
capability. It was extracted from the former RomStorage capability-provider
role.

It exposes:

- `chat-history`
- deterministic record enumeration
- role-based record filtering
- latest-record lookup

Manifest:

- `src/Chat/ChatHistoryProvider/chat-history.provider.json`

## CudaComputeProvider

`CudaComputeProvider` abstracts native CUDA compute modules as AIKernel
provider capabilities. It describes CUDA device profile, entry point, loader
manifest, and native artifact hash metadata.

It is the provider-level boundary for native CUDA implementations such as CUDA
13 modules.

Manifest:

- `src/Compute/CudaComputeProvider/cuda.provider.json`

## DynamicPipelineCompilerProvider

`DynamicPipelineCompilerProvider` exposes a dynamic semantic pipeline compiler
as a provider capability.

It is intended for CLI installation and loading flows such as:

```bash
aik install provider dynamic-pipeline
```

The provider records DSL schema version and schema URI metadata so a host can
validate pipeline compiler compatibility.

Manifest:

- `src/Pipeline/DynamicPipelineCompilerProvider/dynamic-pipeline.provider.json`

## AIKernel.Providers.Standard

`AIKernel.Providers.Standard` is the standard OS driver provider package for
AIKernel hosts. Browser/WASM-specific implementations are kept outside this
package; these drivers focus on host-side OS services.

It includes:

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

`LocalLlmProvider` exposes local LLM runtimes such as Ollama, llama.cpp, and
vLLM through the AIKernel provider boundary.

It records local runtime name, runtime URI, and optional artifact hash metadata.
It allows local inference providers to stay outside Core and Tools while still
participating in AIKernel capability registration.

Manifest:

- `src/Llm/LocalLlmProvider/local-llm.provider.json`

## AIKernel.Providers.MicrosoftAI

`AIKernel.Providers.MicrosoftAI` wraps Microsoft.Extensions.AI based model
execution as an AIKernel provider implementation.

This provider was moved from AIKernel.Core into AIKernel.Providers management
for the 0.1.1 release. Core continues to define contracts; this repository now
owns the implementation, package metadata, tests, documentation, and Python
wrapper reference materials.

The package includes:

- `OpenAICompatibleProvider`
- `OpenAICompatibleProviderOptions`
- `OpenAICompatibleProviderCapabilities`
- `OpenAICompatibleResponseMapper`
- dependency-injection extensions for OpenAI-compatible provider hosting

This provider is consumed as a managed package and DI extension surface rather
than as a standalone manifest in the current release.

## Fail-Closed Implementation Rules

Provider implementations keep public contracts stable while using monadic
composition internally:

- capability invokers return unsupported operations through `Option<T>` and
  structured `CapabilityInvocationResult` errors
- standard drivers use `Option<T>` for missing processes, event subscriptions,
  file-system entries, and provider lookups
- host I/O and network execution remain behind `Result<T>` / `Try.RunAsync`
  boundaries
- provider health and metadata selection use `Either<L,R>` for pure two-way
  decisions

Nullable DTO fields remain at package boundaries where the public contract
requires them; internal decisions should be represented as `Option<T>` before
being projected back to those DTOs.
