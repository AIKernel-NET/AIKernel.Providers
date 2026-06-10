# AIKernel.Providers.MicrosoftAI

[English](README.md)

AIKernel.Providers.MicrosoftAI は、Microsoft.Extensions.AI based model execution
を AIKernel Provider model へ適合させます。

## Role

この Provider は、0.1.1 公開で AIKernel.Core から AIKernel.Providers 管理へ
移管されました。

この移管により、Core は contract と runtime semantics に集中し、この repository
が Microsoft.Extensions.AI implementation package、test、documentation、Python
wrapper への同梱を所有します。

## Public Surface

package には以下が含まれます。

- `OpenAICompatibleProvider`
- `OpenAICompatibleProviderOptions`
- `OpenAICompatibleProviderCapabilities`
- `OpenAICompatibleCredential`
- `OpenAICompatibleResponseMapper`
- provider execution exception type
- dependency-injection hosting extension

## Runtime Behavior

Provider は Microsoft.Extensions.AI `IChatClient` implementation を受け取り、
response を AIKernel Provider result へ投影します。OpenAI 互換 model execution
を可能にしながら、AIKernel の Provider capability contract を維持します。

## Python Inclusion

Python package `aikernel-providers` は `AIKernel.Providers.MicrosoftAI.dll` を
同梱し、`MicrosoftAIProviderOptions` を公開します。これは Core から Providers
への所有移管に従ったものです。

## Dependency Boundary

MicrosoftAIProvider は Microsoft.Extensions.AI と AIKernel contract package を
利用できます。AIKernel.Tools に依存してはいけません。
