# Python Provider Wrapper

[English](index.md)

`aikernel-providers` は、AIKernel 公式拡張 Provider 向けの Python distribution
です。

C# Provider package の wrapper であり、Provider logic を Python で再実装する
ものではありません。wheel は managed assembly を同梱し、統一された Python
import surface を公開します。

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

distribution 名は `aikernel-providers` です。import 名は `aikernel_providers`
です。

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

wheel は provider assembly と manifest file を `aikernel_providers/native` に
同梱します。

- `ChatOpenAIProvider.dll`
- `ChatHistoryProvider.dll`
- `CudaComputeProvider.dll`
- `DynamicPipelineCompilerProvider.dll`
- `LocalLlmProvider.dll`
- `AIKernel.Providers.MicrosoftAI.dll`
- provider manifest JSON file

`provider_assemblies()` は、まず bundled assembly、次に
`AIKERNEL_PROVIDERS_ASSEMBLY_PATH`、最後に global packages cache の matching
NuGet package を解決します。

`load_provider_runtime()` は、解決済み assembly を pythonnet で読み込みます。

## MicrosoftAI Provider

MicrosoftAI support は `MicrosoftAIProviderOptions`、
`MicrosoftAIProviderCapabilities`、response mapping wrapper、および同梱された
`AIKernel.Providers.MicrosoftAI.dll` を通じて Python package に含まれます。

この Provider は 0.1.1 release で AIKernel.Core から AIKernel.Providers 管理へ
移管されました。Python packaging もこの所有変更に従い、公式拡張 Provider
セットとして同梱します。

hosting / dependency-injection 固有の extension method は C# API として残します。
Python wheel には必要な Microsoft.Extensions dependency assembly も同梱し、
Windows / Linux / macOS 上で pythonnet が managed provider surface を一貫して
解決できるようにします。

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

Python wrapper は C# contract mapper と managed Provider object へ委譲します。
host application は得られた contract object を AIKernel capability registry
への Provider 登録に利用します。
