# ChatOpenAIProvider

[English](README.md)

ChatOpenAIProvider は、OpenAI 互換 API 向けの AIKernel.NET 公式外部 Provider
driver です。

## Role

この Provider は、OpenAI 固有の endpoint、model、credential、HTTP client
boundary logic を含みます。AIKernel.Tools を純粋な instrumentation layer として
保つため、Tools から分離されました。

## Capability Surface

Provider manifest は以下を公開します。

- `chat.completion`
- `embedding`
- `moderation`

default provider identity:

- Provider id: `providers.openai`
- Manifest id: `openai.chat`
- Manifest file: `openai.provider.json`

## Manifest Metadata

manifest と settings は決定論的 metadata を保持します。

- endpoint
- model
- optional embedding model
- timeout

API key は runtime configuration であり、manifest へ commit してはいけません。

## Dependency Boundary

ChatOpenAIProvider は AIKernel contract と Core runtime package に依存します。
AIKernel.Tools に依存してはいけません。
