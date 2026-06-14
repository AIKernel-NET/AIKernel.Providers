# AIKernel.Providers Documentation

[English](README.md)

AIKernel.Providers は AIKernel の公式 Provider driver workspace です。external
Provider、standard OS driver、manifest、Python wrapper 参照資料を含み、
AIKernel contract を具体的な service や host 側 driver へ接続します。

この docs は、AIOS SDK の driver model layer として Providers を説明します。
Providers は Core kernel runtime の周囲に、具体的な host service、外部 model
Provider、local runtime、標準 OS driver を組み合わせるための層です。

公式 AIOS ディストリビューション **AIKernel.Monolith** の開発も開始されています。
Monolith は 0.1.x 系の安定化後に Core、Providers、Control、Wasm、GPU backend、
Tools を統合する標準 reference distribution として位置づけられます。

## Sections

- [User Guide](user-guide/index-ja.md)
- [Architecture](architecture/index-ja.md)
- [Provider Catalog](providers/index-ja.md)
- [Provider Development Guidelines](guidelines/provider-development-guidelines-ja.md)
- [Dependency Boundary Checklist](guidelines/dependency-boundary-checklist-ja.md)
- [Python Wrapper](python/index-ja.md)
- [Licensing](licensing/index-ja.md)

## どのページを読むべきか

- install command と公式 Provider の安全な dry-run path を確認する場合は
  User Guide を読んでください。
- ChatOpenAI、ChatHistory、MicrosoftAI、LocalLlm、DynamicPipelineCompiler、
  CudaCompute、Substrate、Council、Audio、Compute、Standard など、どの package を
  選ぶか確認する場合は Provider Catalog を読んでください。
- dependency direction を確認する場合は Architecture を読んでください。Providers は
  driver を実装する層であり、endpoint / native-driver behavior を Core や Tools に
  戻しません。
- Provider を AIKernel.Providers に置くべきか、dedicated package / repository に
  分離すべきか判断する場合は Provider Development Guidelines を読んでください。
- Python boundary を確認する場合は Python Wrapper を読んでください。0.1.1.1 line は
  NuGet-only であり、PyPI package を build / publish しません。

## 最初の安全な検証

live endpoint、credential、native driver を有効化する前に、descriptor、manifest、
dry-run surface で Provider package を検証してください。

```powershell
dotnet build AIKernel.Providers.slnx -c Release
dotnet test AIKernel.Providers.slnx -c Release --no-build
```

## Release Scope

Version 0.1.1.1 は現在の NuGet-only development line です。local package reference
には `0.1.1.1-dev{build-number}` を使います。

Version 0.1.1 は AIKernel.Providers の初回公開 release line です。次を含みます。

- Chat / OpenAI-compatible Provider
- chat history Provider
- CUDA compute Provider metadata surface
- Provider substrate package
- council semantic Provider package
- audio substrate package
- compute metadata substrate package
- dynamic pipeline compiler Provider
- local LLM Provider
- Core から Providers 管理へ移管された MicrosoftAI Provider
- Standard OS driver Provider
- `aikernel-providers` Python wrapper 参照資料
