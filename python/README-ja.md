# aikernel-providers

[English](README.md)

AIKernel 公式拡張 Provider 向け Python wrapper surface です。

0.1.2 正典系列から、`aikernel-providers` は C# AIKernel.Providers assembly が公開する
Provider 契約境界を Python へ公開する PyPI package です。Provider semantics は
Python 側で再実装せず、C# 側の contract surface へ委譲します。

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

Python package は pythonnet が managed assembly を読み込み、Python object は C#
contract surface へ委譲します。

## Managed API Catalog

v0.1.2 package では generated managed API catalog を公開します。
`managed_api_catalog()`、`managed_api_summary()`、`managed_type_names()`、
`find_managed_type(full_name)` で確認できます。

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
