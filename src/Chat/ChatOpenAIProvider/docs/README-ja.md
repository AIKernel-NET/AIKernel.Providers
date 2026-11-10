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

## ストリーミングと埋め込みの実行

`StreamGenerateAsync` は上流のイベントを受信するたびにテキストの差分を渡します。
`StreamCompletionAsync` は上流の終了理由も返します。

`ChatOpenAISettings.EmbeddingModel` を設定し、`ChatOpenAIClient` を渡して
Providerを作成・初期化します。`ITextEmbeddingProvider` と
`IBatchEmbeddingProvider` が上流の埋め込みAPIを呼び、ベクトルを入力順に返します。

Capabilityとして呼ぶ場合は `ChatOpenAIInvoker(provider)` を作成し、
`embedding` の `Arguments["input"]` にテキストを指定します。成功時は
`Metadata["embedding"]` にベクトルのJSON、`Metadata["dimensions"]` に次元数、
`OutputHash` にそのJSONのSHA-256が入ります。マニフェスト検出用の既定コンストラクターでは
Providerの接続が別途必要です。

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
