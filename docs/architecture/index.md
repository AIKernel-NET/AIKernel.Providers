# AIKernel.Providers Architecture

[日本語](index-ja.md)

AIKernel.Providers is the official external provider layer for AIKernel.NET.
It keeps service-specific, runtime-specific, and native-driver-specific logic
outside AIKernel.Core and AIKernel.Tools.

## Layer Boundary

- `AIKernel.NET` / `AIKernel.Abstractions` defines syscall-level OS contracts
  such as compute, process, network, logging, routing, VFS, EventBus, and
  provider contracts.
- `AIKernel.Core` implements deterministic pipeline behavior, DSL, VFS-backed
  capabilities, semantic routing, and Core.Control / Bonsai rule evaluation.
- `AIKernel.Providers.Standard` implements host-side OS drivers for CPU
  compute, file systems, logging, process supervision, network, scheduler,
  profiler, and EventBus.
- `AIKernel.Wasm` owns browser/WebAssembly runtime providers and the WASM
  WebGPU backend.
- `AIKernel.Tools` provides replay, inspection, canonical formatting, CLI, and
  other instrumentation utilities.
- External `AIKernel.Providers` projects implement official extension drivers
  and provider manifests.

This repository must not become a second Core runtime. Providers expose
capabilities and invokers; Core keeps the contract model.

## Inclusion Boundary

AIKernel.Providers may contain provider substrate, manifests, registries,
routers, deterministic fallback policy, runtime-configurable providers, and
pure managed multi-platform .NET dependencies.

Providers that require build-time fixed native runtimes, OS SDKs, browser/WASM
runtimes, vendor SDKs, scenario runtime, or another top-level AIKernel
implementation repository must move to a dedicated package or repository.

See [Provider development guidelines](../guidelines/provider-development-guidelines.md)
and [Dependency boundary checklist](../guidelines/dependency-boundary-checklist.md).

## Provider Types

AIKernel.Providers currently contains these provider categories:

- OpenAI-compatible remote model providers.
- Microsoft.Extensions.AI based managed providers.
- Local LLM runtime providers.
- Native compute providers such as CUDA compute.
- Dynamic compiler providers for semantic pipeline construction.
- Chat history providers for deterministic context records.

## Standard OS Drivers

`AIKernel.Providers.Standard` is the Linux `drivers/` equivalent for AIKernel
OS. It exposes:

- `CpuComputeProvider` for deterministic local compute and CPU fallback.
- `MemoryFileSystemProvider`, `PhysicalFileSystemProvider`, and
  `ZipFileSystemProvider` through the Core `IFileSystemProvider` alias.
- `ConsoleLoggingProvider` and `FileLoggingProvider`.
- `DefaultProcessSupervisorProvider` implementing both process supervision and
  process hosting.
- `HttpNetworkProvider` and `WebSocketNetworkProvider`.
- `SchedulerProvider`, `ProfilerProvider`, and `EventBusProvider`.

These providers are registered through `AddAIKernelStandardProviders()` and are
kept separate from WASM-specific runtime concerns.

## WASM Runtime Layer

`AIKernel.Wasm` completes the browser/WebAssembly layer. It implements WASM
process lifecycle, linear memory, stdin, file surface, events, audio buffer,
screenshot/framebuffer capture, save-state, deterministic time control, and
WebGPU compute through a JavaScript interop backend. When WebGPU is unavailable,
the compute provider delegates fallback execution to
`AIKernel.Providers.Standard.Compute.CpuComputeProvider`.

## CLI OS Commands

`AIKernel.Tools` exposes OS operations through the `aik` command surface:

- `aik ps`, `aik kill <pid-or-name>`, and `aik restart <pid-or-name>`
- `aik logs <process>`
- `aik gpu list` and `aik gpu run vector-add --a a.bin --b b.bin`
- `aik schedule add --every 1m "aik system info"`

The CLI command layer is an operational surface over Core contracts and
standard providers, not a separate runtime.

## Dynamic Loading

Provider manifest files describe the assemblies and capabilities that a host or
CLI can load dynamically. The manifest is intentionally small and deterministic:
it identifies the provider, the assembly, the exported capabilities, metadata,
and CLI hints.

Hosts can copy manifests into a provider directory, resolve the corresponding
assembly, and register the provider through AIKernel's capability registry.

`AIKernel.Providers.Substrate` supplies the shared manifest loader, validator,
registry, and deterministic router used by this flow. Resolution returns
structured results for missing providers, duplicate providers, and explicit
fallback selection.

Forward-compatible manifests may add optional `capabilities`, `metadata`,
`backendMetadata`, `vendorMetadata`, `cli`, and unknown JSON blocks. Unknown
JSON is preserved as raw extension JSON so future schema versions can add
fields without breaking older loaders.

## Council, Audio, And Compute Substrates

Council providers emit semantic material and diagnostics only. They do not own
downstream decision or control-state semantics.

Audio substrate types model audio formats, frames, playback requests, recording
requests, capability descriptors, and validation without backend dependencies.
Dedicated packages own NAudio, SDL, WebAudio, WASM, or OS-specific audio stacks.

Compute substrate types model tensor-like buffer references with standardized
dtype strings, comma-separated shape and stride strings, optional layout, and a
metadata map for backend-specific details.

## MicrosoftAI Migration

`AIKernel.Providers.MicrosoftAI` is managed by this repository starting with the
0.1.1 public release. The implementation was moved from AIKernel.Core so Core
does not own external provider packaging or Microsoft.Extensions.AI integration
details.

The move preserves the same provider contract boundary while changing the
ownership location:

- Core keeps abstractions and runtime contracts.
- AIKernel.Providers owns MicrosoftAI provider implementation, tests, package
  metadata, documentation, and Python wrapper reference materials.

## Dependency Rules

- Providers may depend on AIKernel.NET contracts and AIKernel.Core.
- Providers must not depend on AIKernel.Tools.
- Core must not depend on external providers.
- Tools may consume providers as NuGet packages when needed, but Tools must not
  contain provider-specific capability implementations.

## Python Boundary

`aikernel-providers` is the reserved Python wrapper name for the same provider
boundary. The 0.1.1.1 line is NuGet-only and does not build or publish a PyPI
package. Future Python packaging should load managed assemblies and manifest
JSON files through pythonnet and expose thin wrapper objects.

Python code must not reimplement provider semantics. It delegates to the public
C# package surface.
