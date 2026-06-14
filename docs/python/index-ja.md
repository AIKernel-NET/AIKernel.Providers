# Python Provider Wrapper

[English](index.md)

`aikernel-providers` は、AIKernel 公式拡張 Provider 向けに設計された Python
distribution です。

0.1.1.1 update line では AIKernel.Providers は NuGet-only です。この line では
PyPI package を build / install / publish しません。このページは、将来明示的に
予定される Python release のための reference documentation として残します。

将来の wrapper design は C# Provider package の上に置き、Provider logic を Python
で再実装するものではありません。統一された Python import surface を公開する想定です。

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

0.1.1.1 でサポートされる install command はありません。予約している
distribution 名は `aikernel-providers` です。想定 import 名は
`aikernel_providers` です。

## Scope

過去の package design は public Provider wrapper と helper object を公開する想定です。

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

internal provider helper、private runtime state、public C# package surface に
含まれない Provider 固有 implementation detail は公開しません。

## Managed Assemblies

過去の wheel design では provider assembly と manifest file を
`aikernel_providers/native` に同梱します。

- `ChatOpenAIProvider.dll`
- `ChatHistoryProvider.dll`
- `CudaComputeProvider.dll`
- `DynamicPipelineCompilerProvider.dll`
- `LocalLlmProvider.dll`
- `AIKernel.Providers.MicrosoftAI.dll`
- provider manifest JSON file

`provider_assemblies()` は、まず bundled assembly、次に
`AIKERNEL_PROVIDERS_ASSEMBLY_PATH`、最後に global packages cache の matching
NuGet package を解決する想定です。

`load_provider_runtime()` は、解決済み assembly を pythonnet で読み込む想定です。

## MicrosoftAI Provider

MicrosoftAI support は reference Python wrapper で `MicrosoftAIProviderOptions`、
`MicrosoftAIProviderCapabilities`、response mapping wrapper、および同梱された
`AIKernel.Providers.MicrosoftAI.dll` を通じて表現します。

この Provider は 0.1.1 release で AIKernel.Core から AIKernel.Providers 管理へ
移管されました。将来の Python packaging もこの所有変更に従い、managed provider
surface の薄い wrapper に留めます。

hosting / dependency-injection 固有の extension method は C# API として残します。

## Build

```powershell
cd AIKernel.Providers
dotnet build AIKernel.Providers.slnx -c Release
dotnet test AIKernel.Providers.slnx -c Release --no-build
dotnet pack AIKernel.Providers.slnx -c Release --no-build --no-restore -p:UseLocalPackageVersion=true -p:LocalPackageBuildNumber=1 -o ..\artifacts\local-packages
```

0.1.1.1 では Python build / publish command を実行しません。

## API Example

```python
from aikernel_providers import ChatOpenAICapability, provider_assemblies

contract = ChatOpenAICapability("openai.chat").to_contract()
print(contract.capability_id)
print(contract.provided_operations)

assemblies = provider_assemblies()
print(assemblies.is_complete())
```

将来の Python wrapper は C# contract mapper と managed Provider object へ委譲します。
host application は得られた contract object を AIKernel capability registry への
Provider 登録に利用します。
