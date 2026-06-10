# LocalLlmProvider

[日本語](README-ja.md)

LocalLlmProvider exposes local LLM runtimes through the AIKernel provider
boundary.

## Role

The provider gives AIKernel hosts a stable capability surface for local model
runtimes such as Ollama, llama.cpp, and vLLM. Runtime-specific details remain
outside Core and Tools.

## Capability Surface

The provider exposes:

- `chat.local`
- `embedding.local`

Default provider identity:

- Provider id: `providers.local-llm`
- Manifest id: `local-llm`
- Manifest file: `local-llm.provider.json`

## Manifest Metadata

The manifest and settings record:

- local runtime name
- optional local runtime URI
- optional runtime artifact hash

These values allow a host to describe which local runtime it intends to bind
without making AIKernel.Core depend on that runtime.

## Dependency Boundary

LocalLlmProvider must not call external remote model services by itself and
must not move local runtime implementation details into Tools.
