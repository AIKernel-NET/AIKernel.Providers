# AIKernel.Providers Documentation

[English](README.md)

AIKernel.Providers は AIKernel の公式 Provider driver workspace です。external
Provider、standard OS driver、manifest、Python wrapper を含み、AIKernel contract
を具体的な service や host 側 driver へ接続します。

## Sections

- [User Guide](user-guide/index-ja.md)
- [Architecture](architecture/index-ja.md)
- [Provider Catalog](providers/index-ja.md)
- [Python Wrapper](python/index-ja.md)
- [Licensing](licensing/index-ja.md)

## Release Scope

Version 0.1.1 は AIKernel.Providers の初回公開 release line です。次を含みます。

- Chat / OpenAI-compatible Provider
- chat history Provider
- CUDA compute Provider metadata surface
- dynamic pipeline compiler Provider
- local LLM Provider
- Core から Providers 管理へ移管された MicrosoftAI Provider
- Standard OS driver Provider
- `aikernel-providers` Python wrapper
