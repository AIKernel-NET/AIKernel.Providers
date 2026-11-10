# ChatOpenAIProvider

[日本語](README-ja.md)

ChatOpenAIProvider is the official AIKernel.NET external provider driver for
OpenAI-compatible APIs.

## Role

This provider contains OpenAI-specific endpoint, model, credential, and HTTP
client boundary logic. It was moved out of AIKernel.Tools so Tools can remain a
pure instrumentation layer.

## Capability Surface

The provider manifest exposes:

- `chat.completion`
- `embedding`
- `moderation`

Default provider identity:

- Provider id: `providers.openai`
- Manifest id: `openai.chat`
- Manifest file: `openai.provider.json`

## Streaming and Embedding Execution

`StreamGenerateAsync` delivers text deltas as the upstream event stream arrives.
`StreamCompletionAsync` also returns the upstream finish reason.

Configure `ChatOpenAISettings.EmbeddingModel`, construct the provider with a
`ChatOpenAIClient`, and initialize it. The provider implements
`ITextEmbeddingProvider` and `IBatchEmbeddingProvider` using the upstream
embeddings endpoint. The batch result follows input order.

For a capability invocation, construct `ChatOpenAIInvoker(provider)` and call
`embedding` with the text in `Arguments["input"]`. On success,
`Metadata["embedding"]` contains the JSON float vector, `Metadata["dimensions"]`
contains its dimension, and `OutputHash` is the SHA-256 of the vector JSON.
The default manifest-discovery constructor has no bound provider.

## Manifest Metadata

The manifest and settings carry deterministic metadata:

- endpoint
- model
- optional embedding model
- timeout

API keys are runtime configuration and must not be committed into manifests.

## Dependency Boundary

ChatOpenAIProvider depends on AIKernel contracts and Core runtime packages. It
must not depend on AIKernel.Tools.
