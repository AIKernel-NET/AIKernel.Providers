# LocalLlmProvider

[English](README.md)

LocalLlmProvider は local LLM runtime を AIKernel Provider boundary から公開します。

## Role

この Provider は、Ollama、llama.cpp、vLLM などの local model runtime に対して、
AIKernel host が安定した capability surface を持てるようにします。
runtime-specific detail は Core と Tools の外に残ります。

## Capability Surface

Provider は以下を公開します。

- `chat.local`
- `embedding.local`

default provider identity:

- Provider id: `providers.local-llm`
- Manifest id: `local-llm`
- Manifest file: `local-llm.provider.json`

## Manifest Metadata

manifest と settings は以下を記録します。

- local runtime name
- optional local runtime URI
- optional runtime artifact hash

これらの値により、host は AIKernel.Core を local runtime に依存させず、bind
予定の runtime を記述できます。

## Dependency Boundary

LocalLlmProvider はそれ自体で外部 remote model service を呼び出してはいけません。
また、local runtime implementation detail を Tools へ移してはいけません。
