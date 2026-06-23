# AIKernel.Providers

[日本語 README](README-ja.md)

AIKernel.Providers is the official extension provider workspace for
AIKernel.NET.

AIKernel.Core owns contracts, deterministic runtime boundaries, VFS, DSL, and
standard OS-level providers. AIKernel.Tools owns instrumentation, replay,
inspection, and canonical formatting. AIKernel.Providers owns external provider
drivers that bind AIKernel capability contracts to concrete services, local
runtimes, native modules, or moved provider implementations.

This keeps provider-specific endpoint, credential, runtime, and native-driver
logic out of Core and Tools.

In the AIOS SDK, AIKernel.Providers is the driver model layer. It lets users
assemble concrete OS-style drivers, external model providers, local runtimes,
and standard host services without moving provider-specific logic into the
kernel runtime.

AIKernel also provides an official AIOS distribution, codenamed
**AIKernel.Monolith**. Monolith has begun development as the standard AIOS that
will integrate the SDK layers, including official provider drivers, after the
0.1.x line stabilizes.

## Repository Role

AIKernel.Providers contains official extension providers for the AIKernel
Semantic Runtime.

Providers in this repository implement AIKernel provider contracts from
AIKernel.NET packages and are intended to be loaded by CLI, host applications,
or Python wrappers as external provider modules. They expose capability
descriptors and invocation surfaces while preserving the contract boundary
defined by AIKernel.Core and AIKernel.Abstractions.

OS abstractions such as compute, process supervision, networking, logging,
semantic routing, and file-system aliases live in AIKernel.Abstractions. This
repository keeps only driver implementations such as CPU/CUDA compute,
standard file systems, logging drivers, process supervisor drivers, network
drivers, schedulers, and profilers.

AIKernel.Providers 0.1.3 follows the same development policy as
AIKernel.Core and AIKernel.Control 0.1.3. The line publishes NuGet packages and synchronized Python wrappers. Use`r`n`0.1.3-dev{build-number}` for local NuGet development packages and`r`n`0.1.3.dev{build-number}` for local Python wheel validation until stable publication opens.

## Concept Elevation

AIKernel.Providers follows the common Concept Elevation naming policy
maintained in AIKernel.NET. Providers keep capability adapters, manifests,
routers, DTOs, and provider implementation classes on technical names; concept
vocabulary is limited to concept surfaces and documented compatibility names.

Repository notes: [docs/development/concept-elevation.md](docs/development/concept-elevation.md)

Release notes:

- [English](RELEASE_NOTES.md)
- [日本語](RELEASE_NOTES-ja.md)

## Quick Start

Install only the provider family your host needs. Start with descriptor and
manifest validation before enabling live endpoints, credentials, local model
runtimes, or native drivers.

```bash
dotnet add package AIKernel.Providers.Standard --version 0.1.3
dotnet add package AIKernel.Providers.ChatOpenAI --version 0.1.3
dotnet add package AIKernel.Providers.MicrosoftAI --version 0.1.3
```

Use `AIKernel.Providers.Standard` for OS driver surfaces. Add LLM, chat,
pipeline, CUDA, or MicrosoftAI provider packages only when that capability is
actually part of the host.

## Which Provider Should I Choose?

- Local-only model runtime -> `AIKernel.Providers.LocalLlm`
- OpenAI-compatible API -> `AIKernel.Providers.ChatOpenAI`
- Microsoft.Extensions.AI / Azure AI style integration -> `AIKernel.Providers.MicrosoftAI`
- Lightweight history-backed chat surface -> `AIKernel.Providers.ChatHistory`

Use `AIKernel.Providers.Standard` alongside these when the host needs OS driver
surfaces such as file system, logging, event bus, network, process supervisor,
scheduler, profiler, or CPU compute.

## Providers

Provider projects are grouped by category under `src/`:

- `src/ProviderSubstrate` - provider manifests, registries, deterministic routing, diagnostics, and evidence references.
- `src/Council` - CTG council semantic providers that emit semantic material and diagnostics only.
- `src/Audio` - backend-independent audio substrate for playback and recording providers.
- `src/Llm` - LLM and model-hosting providers.
- `src/Chat` - chat-domain storage and history providers.
- `src/Compute` - native and accelerator compute providers.
- `src/Pipeline` - semantic pipeline compiler and pipeline orchestration providers.
- `src/Standard` - OS standard driver providers.

- `ChatOpenAIProvider` - OpenAI-compatible ChatCompletion, Embedding, and
  Moderation provider manifest surface. This is the official external OpenAI
  provider set extracted from Tools.
- `ChatHistoryProvider` - deterministic chat history provider extracted from
  the former RomStorage provider role.
- `CudaComputeProvider` - abstract provider surface for native CUDA compute
  modules, including CUDA 13 style native capability manifests.
- `DynamicPipelineCompilerProvider` - dynamic semantic pipeline compiler
  provider intended for CLI-driven installation such as
  `aik install provider dynamic-pipeline`.
- `LocalLlmProvider` - local LLM runtime provider for runtimes such as Ollama,
  llama.cpp, and vLLM.
- `AIKernel.Providers.Standard` - standard OS driver provider package containing
  CPU compute, memory/physical/ZIP file system, console/file logging, process
  supervision, event bus, network, scheduler, and profiler providers.
- `AIKernel.Providers.MicrosoftAI` - Microsoft.Extensions.AI based provider
  implementation moved under AIKernel.Providers management from AIKernel.Core.
  This repository now owns its packaging, tests, documentation, and Python
  wrapper inclusion.
- `AIKernel.Providers.Substrate` - pure managed manifest, registry, and router
  substrate with forward-compatible raw JSON extensions.
- `AIKernel.Providers.Council` - Logos / Ethos / Pathos semantic material
  providers for downstream CTG orchestration. Council dimensions use stable
  `logos.*`, `ethos.*`, and `pathos.*` keys for Control normalization and are
  never Gate input.
- `AIKernel.Providers.Audio` - pure managed audio substrate that avoids native,
  OS SDK, browser, and WASM dependencies.
- `AIKernel.Providers.Compute` - backend-neutral compute metadata substrate for
  tensor-like buffer references.

## Provider Manifests

Each external provider includes a JSON manifest that can be copied into a host
provider directory and loaded dynamically:

- `src/Chat/ChatOpenAIProvider/openai.provider.json`
- `src/Chat/ChatHistoryProvider/chat-history.provider.json`
- `src/Compute/CudaComputeProvider/cuda.provider.json`
- `src/Pipeline/DynamicPipelineCompilerProvider/dynamic-pipeline.provider.json`
- `src/Llm/LocalLlmProvider/local-llm.provider.json`
- `src/Standard/AIKernel.Providers.Standard/standard.provider.json`

The manifest records:

- `providerId`
- provider name and version
- managed assembly name
- exported capabilities
- provider metadata needed by host and CLI tooling
- CLI install/list/load hints

MicrosoftAIProvider is consumed as a managed package and dependency-injection
extension surface; it does not currently ship a separate manifest file.

## Python Wrapper Reference

`aikernel-providers` is the reserved Python wrapper name for the official
extension provider set.

The 0.1.3 development line publishes a synchronized PyPI wrapper. Existing
Python materials remain in the repository for reference and future scheduled
Python releases only.

It exposes the C# provider contract boundary as Python objects and helper
functions:

- provider capability descriptors
- manifest and managed assembly discovery
- pythonnet runtime loading
- provider-specific wrapper modules
- bundled provider assemblies and manifest JSON files

The Python wrapper design does not reimplement provider behavior. It remains a
thin wrapper over the managed contract surface.

See:

- [Python provider wrapper](docs/python/index.md)
- [Python provider wrapper 日本語](docs/python/index-ja.md)

## Provider Inclusion Rule

AIKernel.Providers contains provider substrate and runtime-configurable
providers. External dependencies are allowed here only when they are expressed
through configuration, manifests, descriptors, endpoints, or pure managed
multi-platform .NET libraries.

Providers that require build-time fixed native runtimes, OS SDKs, browser/WASM
runtimes, vendor SDKs, scenario state, or another top-level AIKernel
implementation repository must live in a dedicated package or repository.

See [Provider development guidelines](docs/guidelines/provider-development-guidelines.md)
and [Dependency boundary checklist](docs/guidelines/dependency-boundary-checklist.md).

## Dependency Direction

The intended dependency direction is:

- AIKernel.NET defines contracts, DTOs, enums, and Core runtime boundaries.
- AIKernel.Providers implements official external provider drivers.
- AIKernel.Tools consumes providers only as packages when a tool needs an
  external capability.
- Core and Tools do not own provider-specific endpoint, HTTP, credential, or
  native-driver logic.

Rules:

- Providers may depend on AIKernel.NET contract packages and AIKernel.Core.
- Providers must not depend on AIKernel.Tools.
- Tools must not contain provider-specific capability descriptors.
- Core must not depend on external providers.

## Build

```powershell
dotnet build AIKernel.Providers.slnx
dotnet test AIKernel.Providers.slnx
```

Common package metadata is centralized in `Directory.Build.props`.

## Package Installation

For .NET hosts during public release:

```bash
dotnet add package AIKernel.Providers.ChatOpenAI --version 0.1.3
dotnet add package AIKernel.Providers.ChatHistory --version 0.1.3
dotnet add package AIKernel.Providers.Substrate --version 0.1.3
dotnet add package AIKernel.Providers.Council --version 0.1.3
dotnet add package AIKernel.Providers.Audio --version 0.1.3
dotnet add package AIKernel.Providers.Compute --version 0.1.3
dotnet add package AIKernel.Providers.CudaCompute --version 0.1.3
dotnet add package AIKernel.Providers.DynamicPipelineCompiler --version 0.1.3
dotnet add package AIKernel.Providers.LocalLlm --version 0.1.3
dotnet add package AIKernel.Providers.MicrosoftAI --version 0.1.3
```

Python materials are published as synchronized 0.1.3 wrappers for this line.

## Documentation

- [Documentation index](docs/README.md)
- [User Guide](docs/user-guide/index.md)
- [Architecture](docs/architecture/index.md)
- [Provider catalog](docs/providers/index.md)
- [Provider development guidelines](docs/guidelines/provider-development-guidelines.md)
- [Dependency boundary checklist](docs/guidelines/dependency-boundary-checklist.md)
- [Python wrapper](docs/python/index.md)
- [Licensing](docs/licensing/index.md)

## Contributor Guidelines

Provider changes must follow the shared AIKernel development discipline:

- [AIKernel Development Guidelines](../AIKernel.NET/docs/guidelines/AIKERNEL_DEVELOPMENT_GUIDELINES.md)
- [AIKernel 開発ガイドライン](../AIKernel.NET/docs/guidelines/AIKERNEL_DEVELOPMENT_GUIDELINES-jp.md)

Provider implementations should keep endpoint/native-driver behavior outside
Core, expose fail-closed contracts, use monadic LINQ where failures are
composed, and keep Python wrappers aligned with the public C# surface.
