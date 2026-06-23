# AIKernel.Providers Documentation

[日本語](README-ja.md)

AIKernel.Providers is the official provider driver workspace for AIKernel. It
contains external providers, standard OS drivers, manifests, and reference
Python wrapper materials that bind AIKernel contracts to concrete services or
host-side drivers.

These docs describe Providers as the AIOS SDK driver model layer. Providers let
users assemble concrete host services, external model providers, local runtimes,
and standard OS drivers around the Core kernel runtime.

AIKernel.Monolith is the official AIOS distribution now in development. It is
planned as the standard reference distribution that integrates Core, Providers,
Control, Wasm, GPU backends, and Tools after the 0.1.x line stabilizes.

## Cross-Repository Alignment

Shared repository boundaries, v0.1.3 GPU integration versioning, dependency order,
PyPI Trusted Publishing, and Python wrapper scope are defined by
[AIKernel GPU rev3 Migration v0.1.3](https://github.com/AIKernel-NET/AIKernel.NET/blob/main/docs/migration/v0.1.3-gpu-rev3-migration.md).
The historical v0.1.1.1 validation rules remain available in
[AIKernel Repository Alignment v0.1.1.1](https://github.com/AIKernel-NET/AIKernel.NET/blob/main/docs/development/repository-alignment-v0.1.1.1.md).
When a change crosses repositories, start with the
[Cross-Repository Developer Guide v0.1.1.1](https://github.com/AIKernel-NET/AIKernel.NET/blob/main/docs/development/cross-repository-developer-guide-v0.1.1.1.md).

Providers owns substrate, manifests, descriptors, deterministic routing, and
runtime-configurable providers. It must not own Gate decisions, browser/WASM
runtime implementation, fixed native SDK bindings, or scenario semantics.

## Sections

- [User Guide](user-guide/index.md)
- [Architecture](architecture/index.md)
- [Provider Catalog](providers/index.md)
- [Provider Development Guidelines](guidelines/provider-development-guidelines.md)
- [Dependency Boundary Checklist](guidelines/dependency-boundary-checklist.md)
- [Concept Elevation Notes / 概念昇格ノート](development/concept-elevation.md)
- [Python Wrapper](python/index.md)
- [Licensing](licensing/index.md)

## Which Page Should I Read?

- Read the User Guide when you want install commands and the safe dry-run path
  for official Providers.
- Read Provider Catalog when choosing a package such as ChatOpenAI,
  ChatHistory, MicrosoftAI, LocalLlm, DynamicPipelineCompiler, CudaCompute,
  Substrate, Council, Audio, Compute, or Standard.
- Read Architecture when you need to confirm dependency direction: Providers
  implement drivers and must not move endpoint/native-driver behavior into Core
  or Tools.
- Read Provider Development Guidelines when deciding whether a provider belongs
  in AIKernel.Providers or needs a dedicated package / repository.
- Read Python Wrapper when reviewing the thin `aikernel-providers` boundary,
  managed API catalog, and PyPI validation workflow.

## Safe First Validation

Provider packages should be validated through descriptors, manifests, and dry
run surfaces before enabling live endpoints, credentials, or native drivers:

```powershell
dotnet build AIKernel.Providers.slnx -c Release
dotnet test AIKernel.Providers.slnx -c Release --no-build
```

## Release Scope

Version 0.1.3 is the current canonical integration line. Use
`0.1.3-dev{build-number}` for local NuGet package references and
`0.1.3.dev{build-number}` for local Python wheel validation.

Stable package artifacts are created later in dependency order. Do not create
stable `0.1.3` packages until the publication task explicitly requests them.

Version 0.1.2 is the first public release line for AIKernel.Providers. It
contains:

- Chat/OpenAI-compatible providers
- chat history provider
- CUDA compute provider metadata surface
- provider substrate package
- council semantic provider package
- audio substrate package
- compute metadata substrate package
- dynamic pipeline compiler provider
- local LLM provider
- MicrosoftAI provider moved under Providers ownership
- Standard OS driver providers
- `aikernel-providers` Python wrapper with generated managed API catalog
