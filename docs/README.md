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

Shared repository boundaries, 0.1.1.1 local NuGet versioning, and the
NuGet-only / no-PyPI rule for this update line are defined by
[AIKernel Repository Alignment v0.1.1.1](https://github.com/AIKernel-NET/AIKernel.NET/blob/main/docs/development/repository-alignment-v0.1.1.1.md).

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
- Read Python Wrapper when reviewing the reference-only Python boundary. The
  0.1.1.1 line is NuGet-only and does not build or publish a PyPI package.

## Safe First Validation

Provider packages should be validated through descriptors, manifests, and dry
run surfaces before enabling live endpoints, credentials, or native drivers:

```powershell
dotnet build AIKernel.Providers.slnx -c Release
dotnet test AIKernel.Providers.slnx -c Release --no-build
```

## Release Scope

Version 0.1.1.1 is the current NuGet-only development line. Use
`0.1.1.1-dev{build-number}` for local package references.

Version 0.1.1 is the first public release line for AIKernel.Providers. It
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
- `aikernel-providers` Python wrapper reference materials
