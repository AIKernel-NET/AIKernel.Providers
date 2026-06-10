# ChatHistoryProvider

[English](README.md)

ChatHistoryProvider は deterministic chat history record を AIKernel Provider
capability として公開します。

## Role

この Provider は旧 RomStorage Provider role から抽出されました。chat history
access を外部 Provider として保持することで、Core は chat-history storage
implementation detail を所有せず、Tools は instrumentation 専用に保たれます。

## Capability Surface

Provider は以下を公開します。

- `chat.history.read`
- `chat.history.filter`
- `chat.history.latest`

default provider identity:

- Provider id: `chat-history`
- Manifest file: `chat-history.provider.json`

## Data Model

record は deterministic な role / content / timestamp entry として表現されます。
consumer はすべての record を列挙し、role で filter し、sequence から latest
record を解決できます。

## Dependency Boundary

ChatHistoryProvider は AIKernel contract と Core runtime package に依存します。
AIKernel.Tools に依存してはいけません。
