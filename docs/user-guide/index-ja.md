# User Guide

[English](index.md)

この guide は、AIKernel.Providers を .NET host、CLI provider directory、Python
wrapper から利用する方法を説明します。

## Install Packages

```bash
dotnet add package AIKernel.Providers.ChatOpenAI --version 0.1.1
dotnet add package AIKernel.Providers.ChatHistory --version 0.1.1
dotnet add package AIKernel.Providers.CudaCompute --version 0.1.1
dotnet add package AIKernel.Providers.DynamicPipelineCompiler --version 0.1.1
dotnet add package AIKernel.Providers.LocalLlm --version 0.1.1
dotnet add package AIKernel.Providers.MicrosoftAI --version 0.1.1
dotnet add package AIKernel.Providers.Standard --version 0.1.1
```

Python:

```bash
pip install aikernel-providers
```

## Choose a Provider

| Need | Provider |
| --- | --- |
| OpenAI-compatible chat、embedding、moderation | `ChatOpenAIProvider` |
| deterministic chat history record | `ChatHistoryProvider` |
| CUDA / native compute metadata surface | `CudaComputeProvider` |
| dynamic DSL pipeline compile | `DynamicPipelineCompilerProvider` |
| Ollama など local model runtime | `LocalLlmProvider` |
| Microsoft.Extensions.AI compatible execution | `AIKernel.Providers.MicrosoftAI` |
| host-side OS driver | `AIKernel.Providers.Standard` |

## Use Provider Manifests with the CLI

Provider manifest は provider directory に copy し、`aik` から load できます。

```bash
aik providers list --dir ./providers
aik providers capabilities --dir ./providers
aik providers invoke openai.chat chat.completion --dir ./providers prompt=hello
```

最小 manifest 例:

```json
{
  "providerId": "openai.chat",
  "name": "OpenAI Chat Provider",
  "version": "0.1.1",
  "assembly": "ChatOpenAIProvider.dll",
  "capabilities": ["chat.completion"],
  "metadata": {
    "endpoint": "https://api.openai.com/v1",
    "model": "gpt-4o"
  }
}
```

Manifest metadata は deterministic にし、secret を含めません。

## Use Standard OS Drivers

`AIKernel.Providers.Standard` は host-side driver を含みます。

- `CpuComputeProvider`
- `MemoryFileSystemProvider`
- `PhysicalFileSystemProvider`
- `ZipFileSystemProvider`
- `ConsoleLoggingProvider`
- `FileLoggingProvider`
- `DefaultProcessSupervisorProvider`
- `EventBusProvider`
- `HttpNetworkProvider`
- `WebSocketNetworkProvider`
- `SchedulerProvider`
- `ProfilerProvider`

例:

```csharp
using AIKernel.Providers.Standard.Compute;

var compute = new CpuComputeProvider();
var sum = compute.AddVectors([1.0f, 2.0f], [3.0f, 4.0f]);
```

## Use Python Wrappers

```python
from aikernel_providers import (
    ChatOpenAICapability,
    CpuComputeProvider,
    standard_driver_contracts,
)

contract = ChatOpenAICapability("openai.chat").to_contract()
print(contract.capability_id)

for driver in standard_driver_contracts():
    print(driver.provider_id, driver.name)
```

Python package は C# surface の managed wrapper です。Provider behavior は Python
側で再実装しません。

## Failure Behavior

Provider は fail-closed します。

- assembly 不足は明示的な load error にする
- unsupported operation は provider error envelope を返す
- optional backend が unavailable の場合は deterministic fallback metadata を出す
- secret は manifest ではなく host configuration から供給する

## Next Steps

- Provider ごとの scope は [Provider Catalog](../providers/index-ja.md) を参照してください。
- dependency direction を変更する前に [Architecture](../architecture/index-ja.md) を確認してください。
- wheel 公開前に [Python Wrapper](../python/index-ja.md) を確認してください。
