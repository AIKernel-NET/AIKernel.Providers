# Provider Catalog

[日本語](index-ja.md)

This catalog describes the provider packages included in the 0.1.1
AIKernel.Providers release.

Provider projects are grouped under `src/` by category:

- `Llm` - LLM and model-hosting providers.
- `Chat` - chat-domain storage and history providers.
- `Compute` - native and accelerator compute providers.
- `Pipeline` - semantic pipeline compiler and pipeline orchestration providers.
- `Standard` - OS standard driver providers.

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
wrapper inclusion.

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
