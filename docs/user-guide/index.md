# User Guide

[日本語](index-ja.md)

This guide explains how to consume AIKernel.Providers from .NET hosts, CLI
provider directories, and Python wrappers.

Providers are the AIOS SDK driver model layer. Use them when a distribution
needs concrete OS-style drivers, external model providers, local runtimes,
standard host services, or dynamically loaded provider manifests.

AIKernel.Monolith is the official AIOS distribution now in development. It will
serve as the standard reference distribution that integrates official provider
drivers with the rest of the SDK after the 0.1.x line stabilizes.

## Install Packages

```bash
dotnet add package AIKernel.Providers.ChatOpenAI --version 0.1.1.1
dotnet add package AIKernel.Providers.ChatHistory --version 0.1.1.1
dotnet add package AIKernel.Providers.CudaCompute --version 0.1.1.1
dotnet add package AIKernel.Providers.DynamicPipelineCompiler --version 0.1.1.1
dotnet add package AIKernel.Providers.LocalLlm --version 0.1.1.1
dotnet add package AIKernel.Providers.MicrosoftAI --version 0.1.1.1
dotnet add package AIKernel.Providers.Standard --version 0.1.1.1
```

The 0.1.1.1 validation line is NuGet-only. Do not build, install, or publish a
PyPI package for this line. Prepare the Python wrapper for the next official
v0.1.2 canonical series, where synchronized NuGet and PyPI package families are
expected.

## Choose a Provider

| Need | Provider |
| --- | --- |
| OpenAI-compatible chat, embeddings, moderation | `ChatOpenAIProvider` |
| Deterministic chat history records | `ChatHistoryProvider` |
| CUDA/native compute metadata surface | `CudaComputeProvider` |
| Dynamic DSL pipeline compilation | `DynamicPipelineCompilerProvider` |
| Local model runtime such as Ollama | `LocalLlmProvider` |
| Microsoft.Extensions.AI compatible execution | `AIKernel.Providers.MicrosoftAI` |
| Host-side OS drivers | `AIKernel.Providers.Standard` |

## Use Provider Manifests with the CLI

Provider manifests can be copied into a provider directory and loaded by `aik`:

```bash
aik providers list --dir ./providers
aik providers capabilities --dir ./providers
aik providers invoke openai.chat chat.completion --dir ./providers prompt=hello
```

A minimal manifest shape:

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

Manifest metadata should be deterministic and should not contain secrets.

## Use Standard OS Drivers

`AIKernel.Providers.Standard` contains host-side drivers:

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

Example:

```csharp
using AIKernel.Providers.Standard.Compute;

var compute = new CpuComputeProvider();
var sum = compute.AddVectors([1.0f, 2.0f], [3.0f, 4.0f]);
```

## Python Wrapper Reference

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

Python wrapper materials are reference-only for 0.1.1.1. They describe a future
managed wrapper over the C# surface and must not re-implement provider behavior
in Python.

## Failure Behavior

Providers should fail closed:

- missing assemblies should produce explicit load errors
- unsupported operations should return provider error envelopes
- unavailable optional backends should surface deterministic fallback metadata
- secrets should be supplied through host configuration, not manifests

## Next Steps

- Read the [Provider Catalog](../providers/index.md) for per-provider scope.
- Read [Architecture](../architecture/index.md) before changing dependency
  direction.
- Read [Python Wrapper](../python/index.md) for the reference-only Python
  wrapper boundary.
