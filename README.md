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

AIKernel.Providers participates in the 0.1.1 public release scheduled
for 2026-06-10. Version `0.1.1` is the first public release of this repository.
Development builds may be used for local validation, but user-facing package
history is written only for public releases; development changes are merged
into the next public release note.

Release notes:

- [English](RELEASE_NOTES.md)
- [日本語](RELEASE_NOTES-ja.md)

## Quick Start

Install only the provider family your host needs. Start with descriptor and
manifest validation before enabling live endpoints, credentials, local model
runtimes, or native drivers.

```bash
dotnet add package AIKernel.Providers.Standard --version 0.1.1
dotnet add package AIKernel.Providers.ChatOpenAI --version 0.1.1
dotnet add package AIKernel.Providers.MicrosoftAI --version 0.1.1
```

For Python hosts:

```bash
pip install aikernel-providers
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

## Provider Manifests

Each external provider includes a JSON manifest that can be copied into a host
provider directory and loaded dynamically:

- `src/Llm/ChatOpenAIProvider/openai.provider.json`
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

## Python Package

`aikernel-providers` is the Python wrapper package for the official extension
provider set.

It exposes the C# provider contract boundary as Python objects and helper
functions:

- provider capability descriptors
- manifest and managed assembly discovery
- pythonnet runtime loading
- provider-specific wrapper modules
- bundled provider assemblies and manifest JSON files

The Python package does not reimplement provider behavior. It bundles the C#
assemblies under `aikernel_providers/native` and delegates to the managed
contract surface.

See:

- [Python provider wrapper](docs/python/index.md)
- [Python provider wrapper 日本語](docs/python/index-ja.md)

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
dotnet add package AIKernel.Providers.ChatOpenAI --version 0.1.1
dotnet add package AIKernel.Providers.ChatHistory --version 0.1.1
dotnet add package AIKernel.Providers.CudaCompute --version 0.1.1
dotnet add package AIKernel.Providers.DynamicPipelineCompiler --version 0.1.1
dotnet add package AIKernel.Providers.LocalLlm --version 0.1.1
dotnet add package AIKernel.Providers.MicrosoftAI --version 0.1.1
```

For Python hosts:

```bash
pip install aikernel-providers
```

Import the Python module as `aikernel_providers`:

```python
from aikernel_providers import (
    ChatOpenAICapability,
    ChatHistoryCapability,
    CudaComputeCapability,
    DynamicPipelineCompilerCapability,
    LocalLlmCapability,
    MicrosoftAIProviderOptions,
)
```

The wheel bundles managed provider assemblies under
`aikernel_providers/native`. It is a wrapper over the public C# provider
surface, not a separate Python implementation of provider semantics.

## Documentation

- [Documentation index](docs/README.md)
- [User Guide](docs/user-guide/index.md)
- [Architecture](docs/architecture/index.md)
- [Provider catalog](docs/providers/index.md)
- [Python wrapper](docs/python/index.md)
- [Licensing](docs/licensing/index.md)

## Contributor Guidelines

Provider changes must follow the shared AIKernel development discipline:

- [AIKernel Development Guidelines](../AIKernel.NET/docs/guidelines/AIKERNEL_DEVELOPMENT_GUIDELINES.md)
- [AIKernel 開発ガイドライン](../AIKernel.NET/docs/guidelines/AIKERNEL_DEVELOPMENT_GUIDELINES-jp.md)

Provider implementations should keep endpoint/native-driver behavior outside
Core, expose fail-closed contracts, use monadic LINQ where failures are
composed, and keep Python wrappers aligned with the public C# surface.
