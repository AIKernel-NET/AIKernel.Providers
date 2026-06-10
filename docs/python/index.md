# Python Provider Wrapper

[日本語](index-ja.md)

`aikernel-providers` is the Python distribution for official AIKernel extension
providers.

It is a wrapper over C# provider packages, not a Python reimplementation of
provider logic. The wheel bundles managed assemblies and exposes a unified
Python import surface:

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

```bash
pip install aikernel-providers
```

The distribution name is `aikernel-providers`. The import name is
`aikernel_providers`.

## Scope

The package exposes public provider wrappers and helper objects:

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

The wheel bundles provider assemblies and manifest files under
`aikernel_providers/native`:

- `ChatOpenAIProvider.dll`
- `ChatHistoryProvider.dll`
- `CudaComputeProvider.dll`
- `DynamicPipelineCompilerProvider.dll`
- `LocalLlmProvider.dll`
- `AIKernel.Providers.MicrosoftAI.dll`
- provider manifest JSON files

`provider_assemblies()` resolves bundled assemblies first, then paths from
`AIKERNEL_PROVIDERS_ASSEMBLY_PATH`, then matching NuGet packages from the global
packages cache.

`load_provider_runtime()` loads the resolved assemblies through pythonnet.

## MicrosoftAI Provider

MicrosoftAI support is included in the Python package through
`MicrosoftAIProviderOptions`, `MicrosoftAIProviderCapabilities`, response
mapping wrappers, and the bundled `AIKernel.Providers.MicrosoftAI.dll`.

This provider was moved from AIKernel.Core into AIKernel.Providers management
for the 0.1.1 release. Python packaging follows that ownership change and
bundles the provider with the official extension provider set.

Hosting and dependency-injection extension methods remain C# APIs. The Python
wheel still bundles the required Microsoft.Extensions dependency assemblies so
pythonnet can resolve the managed provider surface consistently on Windows,
Linux, and macOS.

## Build

```powershell
cd AIKernel.Providers
dotnet build AIKernel.Providers.slnx -c Release
dotnet test AIKernel.Providers.slnx -c Release --no-build
cd python
py -m pytest
py -m build
```

## API Example

```python
from aikernel_providers import ChatOpenAICapability, provider_assemblies

contract = ChatOpenAICapability("openai.chat").to_contract()
print(contract.capability_id)
print(contract.provided_operations)

assemblies = provider_assemblies()
print(assemblies.is_complete())
```

The Python wrapper delegates to C# contract mappers and managed provider
objects. Host applications should use the resulting contract objects to
register providers with their AIKernel capability registry.
