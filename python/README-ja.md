# aikernel-providers

[English](README.md)

AIKernel 公式拡張 Provider 向け Python wrapper surface の参照設計です。

0.1.1.1 系では、AIKernel.Providers は NuGet package のみを公開対象とします。
この directory は将来の Python wrapper surface の参照資料として維持し、
PyPI package として build / install / publish は行いません。

`aikernel-providers` は、C# AIKernel.Providers assembly が公開する Provider 契約境界を
Python へ公開するための予約名です。Provider semantics は Python 側で再実装せず、
C# 側の contract surface へ委譲する想定です。

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

将来 Python package を作成する場合は、pythonnet が managed assembly を読み込み、
Python object は C# contract surface へ委譲します。

参照 package には、public な Provider、Invoker、Capability descriptor、
Settings、MicrosoftAI の option / capability / response mapping surface、
および `AIKernel.Providers.Standard` OS driver descriptor の wrapper が含まれます。
C# hosting 固有の extension method は C# API として残します。

## Included Providers

- ChatOpenAIProvider
- ChatHistoryProvider
- CudaComputeProvider
- DynamicPipelineCompilerProvider
- LocalLlmProvider
- AIKernel.Providers.MicrosoftAI
- AIKernel.Providers.Standard

MicrosoftAI support は、0.1.1 release でこの Provider の所有が AIKernel.Core から
AIKernel.Providers へ移管されたため、reference wrapper に含まれます。

package scope の詳細は repository documentation を参照してください。

- `docs/python/index-ja.md`
- `docs/providers/index-ja.md`
