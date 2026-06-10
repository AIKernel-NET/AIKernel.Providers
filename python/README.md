# aikernel-providers

[日本語](README-ja.md)

Official Python wrappers for AIKernel extension providers.

`aikernel-providers` exposes provider contract boundaries from the C#
AIKernel.Providers assemblies without re-implementing provider semantics in
Python.

## Import Surface

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
    CpuComputeProvider,
    standard_driver_contracts,
    provider_assemblies,
)
```

## Example

```python
from aikernel_providers import ChatOpenAICapability, provider_assemblies

capability = ChatOpenAICapability("openai.chat").to_contract()
print(capability.capability_id)
print(capability.provided_operations)

assemblies = provider_assemblies()
print(assemblies.is_complete())
```

The wheel bundles provider assemblies when built from the repository package
workflow. At runtime, pythonnet loads the managed assemblies and Python objects
delegate to the C# contract surface.

The Python package includes wrappers for the public Provider, Invoker,
Capability descriptor, Settings, MicrosoftAI option/capability/response mapping
surfaces, and `AIKernel.Providers.Standard` OS driver descriptors. C# hosting-
specific extension methods remain C# APIs, while the wheel still bundles their
assembly dependencies so the managed runtime can resolve them on Windows,
Linux, and macOS.

## Included Providers

- ChatOpenAIProvider
- ChatHistoryProvider
- CudaComputeProvider
- DynamicPipelineCompilerProvider
- LocalLlmProvider
- AIKernel.Providers.MicrosoftAI
- AIKernel.Providers.Standard

MicrosoftAI support is included because ownership of that provider moved from
AIKernel.Core to AIKernel.Providers for the 0.1.1 release.

See the repository documentation for the full package scope:

- `docs/python/index.md`
- `docs/providers/index.md`
