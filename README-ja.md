# AIKernel.Providers

[English README](README.md)

AIKernel.Providers は、AIKernel.NET 向けの公式拡張 Provider ワークスペースです。

AIKernel.Core は contract、決定論的 runtime boundary、VFS、DSL、標準 OS-level
Provider を所有します。AIKernel.Tools は instrumentation、replay、inspection、
canonical formatting を所有します。AIKernel.Providers は、AIKernel capability
contract を具体的な service、local runtime、native module、または移管済み
Provider 実装へ接続する外部 Provider driver を所有します。

これにより、Provider 固有の endpoint、credential、runtime、native driver
logic を Core と Tools から分離します。

AIOS SDK において、AIKernel.Providers は driver model layer です。具体的な
OS-style driver、外部 model Provider、local runtime、標準 host service を組み合わせ、
Provider 固有の logic を kernel runtime に戻さずに独自の AIOS を構築できます。

AIKernel には、公式 AIOS ディストリビューションである **AIKernel.Monolith** もあります。
Monolith は 0.1.x 系の安定化後に、公式 Provider driver を含む SDK layer を
統合する標準 AIOS として開発が開始されています。

## リポジトリの役割

AIKernel.Providers は、AIKernel Semantic Runtime 向けの公式拡張 Provider を
収容します。

この repository の Provider は、AIKernel.NET package が定義する Provider
contract を実装し、CLI、host application、Python wrapper から外部 Provider
module として読み込まれることを想定しています。AIKernel.Core と
AIKernel.Abstractions が定義する contract boundary を維持しながら、capability
descriptor と invocation surface を公開します。

AIKernel.Providers は、2026-06-10 に予定している 0.1.1 公開に参加します。
`0.1.1` はこの repository の初版公開です。local validation で development build
を使う場合がありますが、利用者向けの package history は公開 release のみを単位とし、
開発中の変更は次の公開 release note に統合して記載します。

リリースノート:

- [English](RELEASE_NOTES.md)
- [日本語](RELEASE_NOTES-ja.md)

## クイックスタート

host が必要とする Provider family だけを導入してください。live endpoint、
credential、local model runtime、native driver を有効化する前に、まず descriptor と
manifest の検証から始めます。

```bash
dotnet add package AIKernel.Providers.Standard --version 0.1.1
dotnet add package AIKernel.Providers.ChatOpenAI --version 0.1.1
dotnet add package AIKernel.Providers.MicrosoftAI --version 0.1.1
```

Python host:

```bash
pip install aikernel-providers
```

OS driver surface が必要な場合は `AIKernel.Providers.Standard` を使用します。LLM、
chat、pipeline、CUDA、MicrosoftAI の Provider package は、その capability が host に
必要な場合だけ追加してください。

## どの Provider を選ぶべきか

- ローカルだけで model runtime を動かしたい -> `AIKernel.Providers.LocalLlm`
- OpenAI-compatible API を使いたい -> `AIKernel.Providers.ChatOpenAI`
- Microsoft.Extensions.AI / Azure AI 系 integration を使いたい -> `AIKernel.Providers.MicrosoftAI`
- 履歴ベースの軽量 chat surface を使いたい -> `AIKernel.Providers.ChatHistory`

file system、logging、event bus、network、process supervisor、scheduler、
profiler、CPU compute などの OS driver surface が必要な host では、
これらに加えて `AIKernel.Providers.Standard` を使用してください。

## Provider 構成

Provider project は `src/` 配下で category ごとに整理します。

- `src/Llm` - LLM / model hosting 系 Provider。
- `src/Chat` - chat domain の storage / history 系 Provider。
- `src/Compute` - native / accelerator compute 系 Provider。
- `src/Pipeline` - semantic pipeline compiler / pipeline orchestration 系 Provider。
- `src/Standard` - OS 標準 driver Provider。

- `ChatOpenAIProvider` - OpenAI 互換 ChatCompletion、Embedding、Moderation
  向け Provider manifest surface です。Tools から分離した公式外部 OpenAI
  Provider セットです。
- `ChatHistoryProvider` - 旧 RomStorage Provider の役割から抽出された、
  deterministic chat history Provider です。
- `CudaComputeProvider` - CUDA 13 系 native capability manifest を含む、
  native CUDA compute module 向けの抽象 Provider surface です。
- `DynamicPipelineCompilerProvider` - `aik install provider dynamic-pipeline`
  のような CLI-driven install を想定した dynamic semantic pipeline compiler
  Provider です。
- `LocalLlmProvider` - Ollama、llama.cpp、vLLM などの local LLM runtime
  向け Provider です。
- `AIKernel.Providers.Standard` - CPU compute、memory / physical / ZIP file
  system、console / file logging、process supervision、event bus、network、
  scheduler、profiler を含む OS 標準 driver Provider package です。
- `AIKernel.Providers.MicrosoftAI` - Microsoft.Extensions.AI を利用した
  Provider 実装です。これは AIKernel.Core から AIKernel.Providers 管理へ
  移管されました。今後はこの repository が packaging、test、documentation、
  Python wrapper への同梱を所有します。

## Provider Manifest

各外部 Provider は、host の provider directory へコピーして動的ロードできる
JSON manifest を持ちます。

- `src/Chat/ChatOpenAIProvider/openai.provider.json`
- `src/Chat/ChatHistoryProvider/chat-history.provider.json`
- `src/Compute/CudaComputeProvider/cuda.provider.json`
- `src/Pipeline/DynamicPipelineCompilerProvider/dynamic-pipeline.provider.json`
- `src/Llm/LocalLlmProvider/local-llm.provider.json`
- `src/Standard/AIKernel.Providers.Standard/standard.provider.json`

manifest には以下を記録します。

- `providerId`
- Provider 名と version
- managed assembly 名
- 公開 capability
- host と CLI tooling に必要な provider metadata
- CLI install / list / load 用の hint

MicrosoftAIProvider は managed package と dependency-injection extension surface
として利用されます。現時点では個別の manifest file は同梱しません。

## Python Package

`aikernel-providers` は、公式拡張 Provider セットの Python wrapper package です。

C# Provider contract boundary を Python object と helper function として公開します。

- provider capability descriptor
- manifest と managed assembly discovery
- pythonnet runtime loading
- provider-specific wrapper module
- 同梱 provider assembly と manifest JSON file

Python package は Provider behavior を再実装しません。C# assembly を
`aikernel_providers/native` に同梱し、managed contract surface へ委譲します。

関連ドキュメント:

- [Python provider wrapper](docs/python/index.md)
- [Python provider wrapper 日本語](docs/python/index-ja.md)

## 依存関係の方向

想定する依存方向は以下です。

- AIKernel.NET が contract、DTO、enum、Core runtime boundary を定義します。
- AIKernel.Providers が公式外部 Provider driver を実装します。
- AIKernel.Tools は、外部 capability が必要な tool で Provider を package として
  参照します。
- Core と Tools は、Provider 固有の endpoint、HTTP、credential、native-driver
  logic を所有しません。

ルール:

- Provider は AIKernel.NET contract package と AIKernel.Core に依存できます。
- Provider は AIKernel.Tools に依存してはいけません。
- Tools は Provider 固有の capability descriptor を含めてはいけません。
- Core は外部 Provider に依存してはいけません。

## ビルド

```powershell
dotnet build AIKernel.Providers.slnx
dotnet test AIKernel.Providers.slnx
```

共通 package metadata は `Directory.Build.props` に集約されています。

## パッケージインストール

.NET host では、公開後に NuGet package を使用します。

```bash
dotnet add package AIKernel.Providers.ChatOpenAI --version 0.1.1
dotnet add package AIKernel.Providers.ChatHistory --version 0.1.1
dotnet add package AIKernel.Providers.CudaCompute --version 0.1.1
dotnet add package AIKernel.Providers.DynamicPipelineCompiler --version 0.1.1
dotnet add package AIKernel.Providers.LocalLlm --version 0.1.1
dotnet add package AIKernel.Providers.MicrosoftAI --version 0.1.1
```

Python host では PyPI package を使用します。

```bash
pip install aikernel-providers
```

Python module は `aikernel_providers` として import します。

```python
from aikernel_providers import (
    ChatOpenAICapability,
    ChatHistoryCapability,
    CudaComputeCapability,
    DynamicPipelineCompilerCapability,
    LocalLlmCapability,
    MicrosoftAIProviderOptions,
)
```

wheel は managed provider assemblies を `aikernel_providers/native` に同梱します。
これは public C# Provider surface への wrapper であり、Provider semantics を
Python で別実装するものではありません。

## ドキュメント

- [Documentation index](docs/README-ja.md)
- [User Guide](docs/user-guide/index-ja.md)
- [Architecture](docs/architecture/index-ja.md)
- [Provider catalog](docs/providers/index-ja.md)
- [Python wrapper](docs/python/index-ja.md)
- [Licensing](docs/licensing/index-ja.md)

## コントリビュータ向けガイドライン

Provider の変更は、AIKernel 共通の開発規律に従ってください。

- [AIKernel 開発ガイドライン](../AIKernel.NET/docs/guidelines/AIKERNEL_DEVELOPMENT_GUIDELINES-jp.md)
- [AIKernel Development Guidelines](../AIKernel.NET/docs/guidelines/AIKERNEL_DEVELOPMENT_GUIDELINES.md)

Provider implementation は endpoint / native-driver behavior を Core の外に保ち、
fail-closed contract を公開し、failure composition には monadic LINQ を使用し、
Python wrapper を public C# surface と整合させてください。
