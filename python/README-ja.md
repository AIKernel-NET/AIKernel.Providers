# aikernel-providers

[English](README.md)

AIKernel 公式拡張 Provider 向けの Python wrapper です。

`aikernel-providers` は、C# AIKernel.Providers assembly が公開する Provider
契約境界を Python へ公開します。Provider semantics は Python 側で再実装せず、
C# 側の contract surface へ委譲します。

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

repository の package workflow で build すると、provider assembly が wheel へ
同梱されます。runtime では pythonnet が managed assembly を読み込み、Python
object は C# contract surface へ委譲します。

Python package には、public な Provider、Invoker、Capability descriptor、
Settings、MicrosoftAI の option / capability / response mapping surface、
および `AIKernel.Providers.Standard` OS driver descriptor の wrapper が含まれます。
C# hosting 固有の extension method は C# API として残しますが、managed runtime
が Windows / Linux / macOS 上で解決できるように、必要な assembly dependency は
wheel に同梱します。

## Included Providers

- ChatOpenAIProvider
- ChatHistoryProvider
- CudaComputeProvider
- DynamicPipelineCompilerProvider
- LocalLlmProvider
- AIKernel.Providers.MicrosoftAI
- AIKernel.Providers.Standard

MicrosoftAI support は、0.1.1 release でこの Provider の所有が AIKernel.Core から
AIKernel.Providers へ移管されたため、この package に含まれます。

package scope の詳細は repository documentation を参照してください。

- `docs/python/index-ja.md`
- `docs/providers/index-ja.md`
