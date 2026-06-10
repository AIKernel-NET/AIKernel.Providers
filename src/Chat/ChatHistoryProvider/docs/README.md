# ChatHistoryProvider

[日本語](README-ja.md)

ChatHistoryProvider exposes deterministic chat history records as an AIKernel
provider capability.

## Role

This provider was extracted from the former RomStorage provider role. It keeps
chat history access as an external provider so Core does not own chat-history
storage implementation details and Tools remains instrumentation-only.

## Capability Surface

The provider exposes:

- `chat.history.read`
- `chat.history.filter`
- `chat.history.latest`

Default provider identity:

- Provider id: `chat-history`
- Manifest file: `chat-history.provider.json`

## Data Model

Records are represented as deterministic role/content/timestamp entries.
Consumers can enumerate all records, filter by role, or resolve the latest
record from a sequence.

## Dependency Boundary

ChatHistoryProvider depends on AIKernel contracts and Core runtime packages. It
must not depend on AIKernel.Tools.
