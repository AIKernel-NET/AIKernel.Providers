# Python Provider Wrapper

[日本語](index-ja.md)

`aikernel-providers` was designed as the Python distribution for official
AIKernel extension providers.

For the 0.1.1.1 update line, AIKernel.Providers is NuGet-only. Do not build,
install, or publish a PyPI package for this line. This page is retained as
reference documentation for a future explicitly scheduled Python release.

The future wrapper design sits over C# provider packages; it is not a Python
reimplementation of provider logic. It should expose a unified Python import
surface:

```python
from aikernel_providers import (
    ChatOpenAICapability,
    ChatOpenAIProvider,
    ChatOpenAISettings,
    ChatHistoryCapability,
    ChatHistoryProvider,
    ChatHistorySettings,
    CudaComputeCapability,
    CudaComputeProvider,
    CudaComputeSettings,
    DynamicPipelineCompilerCapability,
    DynamicPipelineCompilerProvider,
    DynamicPipelineCompilerSettings,
    LocalLlmCapability,
    LocalLlmProvider,
    LocalLlmSettings,
    MicrosoftAIProvider,
    MicrosoftAIProviderCapabilities,
    MicrosoftAIProviderOptions,
    provider_assemblies,
)
```

## Install

There is no supported install command for 0.1.1.1. The reserved distribution
name is `aikernel-providers`. The intended import name is
`aikernel_providers`.

## Scope

The archived package design exposes public provider wrappers and helper
objects:

- `CapabilityContract`
- `ChatOpenAICapability`, `ChatOpenAIProvider`, `ChatOpenAIInvoker`,
  `ChatOpenAISettings`, `ChatOpenAIClient`
- `ChatHistoryCapability`, `ChatHistoryProvider`, `ChatHistoryInvoker`,
  `ChatHistorySettings`, `ChatHistoryRecord`, `ChatMessage`, `ChatHistory`
- `CudaComputeCapability`, `CudaComputeProvider`, `CudaComputeInvoker`,
  `CudaComputeSettings`
- `DynamicPipelineCompilerCapability`,
  `DynamicPipelineCompilerProvider`, `DynamicPipelineCompilerInvoker`,
  `DynamicPipelineCompilerSettings`
- `LocalLlmCapability`, `LocalLlmProvider`, `LocalLlmInvoker`,
  `LocalLlmSettings`
- `MicrosoftAIProvider`, `MicrosoftAIProviderOptions`,
  `MicrosoftAIProviderCapabilities`, `MicrosoftAICredential`,
  `MicrosoftAIHealthContext`, `MicrosoftAIResponseMapper`,
  `MicrosoftAIResponseProjection`, `MicrosoftAIProviderOptionsValidator`
- `provider_assemblies()`
- `load_provider_runtime()`

It does not expose internal provider helpers, private runtime state, or
provider-specific implementation details that are not part of the public C#
package surface.

## Managed Assemblies

The archived wheel design bundles provider assemblies and manifest files under
`aikernel_providers/native`:

- `ChatOpenAIProvider.dll`
- `ChatHistoryProvider.dll`
- `CudaComputeProvider.dll`
- `DynamicPipelineCompilerProvider.dll`
- `LocalLlmProvider.dll`
- `AIKernel.Providers.MicrosoftAI.dll`
- provider manifest JSON files

`provider_assemblies()` is intended to resolve bundled assemblies first, then
paths from `AIKERNEL_PROVIDERS_ASSEMBLY_PATH`, then matching NuGet packages
from the global packages cache.

`load_provider_runtime()` is intended to load the resolved assemblies through
pythonnet.

## MicrosoftAI Provider

MicrosoftAI support is represented in the reference Python wrapper through
`MicrosoftAIProviderOptions`, `MicrosoftAIProviderCapabilities`, response
mapping wrappers, and the bundled `AIKernel.Providers.MicrosoftAI.dll`.

This provider was moved from AIKernel.Core into AIKernel.Providers management
for the 0.1.1 release. Future Python packaging should follow that ownership
change and keep the wrapper thin over the managed provider surface.

Hosting and dependency-injection extension methods remain C# APIs.

## Build

```powershell
cd AIKernel.Providers
dotnet build AIKernel.Providers.slnx -c Release
dotnet test AIKernel.Providers.slnx -c Release --no-build
dotnet pack AIKernel.Providers.slnx -c Release --no-build --no-restore -p:UseLocalPackageVersion=true -p:LocalPackageBuildNumber=1 -o ..\artifacts\local-packages
```

Do not run Python build or publish commands for 0.1.1.1.

## API Example

```python
from aikernel_providers import ChatOpenAICapability, provider_assemblies

contract = ChatOpenAICapability("openai.chat").to_contract()
print(contract.capability_id)
print(contract.provided_operations)

assemblies = provider_assemblies()
print(assemblies.is_complete())
```

The future Python wrapper must delegate to C# contract mappers and managed
provider objects. Host applications should use the resulting contract objects to
register providers with their AIKernel capability registry.
