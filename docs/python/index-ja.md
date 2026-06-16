# Python Provider Wrapper

[English](index.md)

`aikernel-providers` は、0.1.2 正典シリーズで AIKernel 公式拡張 Provider 向けに
公開する Python distribution です。

wrapper は C# Provider package の上に置き、Provider logic を Python で再実装する
ものではありません。統一された Python import surface を公開します。

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

安定版 0.1.2 の公開タスク開始後は次で導入します。

```bash
pip install aikernel-providers==0.1.2
```

local validation では、local package output から一致する
`0.1.2.dev<build-number>` wheel を導入します。

## Scope

package は public Provider wrapper と helper object を公開します。

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

wheel では provider assembly と manifest file を `aikernel_providers/native` に同梱します。

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
移管されました。0.1.2 の Python wrapper もこの所有変更に従い、managed provider
surface の薄い wrapper に留めます。

hosting / dependency-injection 固有の extension method は C# API として残します。

## Build

```powershell
cd AIKernel.Providers
dotnet build AIKernel.Providers.slnx -c Release
dotnet test AIKernel.Providers.slnx -c Release --no-build
dotnet pack AIKernel.Providers.slnx -c Release --no-build --no-restore -p:UseLocalPackageVersion=true -p:LocalPackageBuildNumber=1 -o ..\artifacts\local-packages
```

local validation では `0.1.2.dev<build-number>` の Python wheel だけを build します。
安定版 `0.1.2` wheel は公開タスク開始後に作成します。

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
