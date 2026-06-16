# Dependency Boundary Checklist

[English](dependency-boundary-checklist.md)

AIKernel.Providers に Provider 実装を追加または受け入れる前に、この checklist を確認します。

## Repository Role

AIKernel.Providers が所有するもの:

- provider substrate
- provider manifest
- provider registry / router / resolution policy
- runtime-configurable provider
- pure managed multi-platform provider implementation
- dedicated backend package 向け descriptor / invoker boundary
- Providers repository boundary に対応する Python wrapper 参照資料

AIKernel.Providers が所有しないもの:

- Core deterministic governance logic
- Control orchestration logic
- Tools CLI / inspection implementation
- browser / WASM runtime implementation
- CUDA native runtime implementation
- Windows SDK / WinRT implementation
- scenario-specific runtime implementation

## Allowed Dependencies

AIKernel.Providers が参照してよいもの:

- AIKernel.NET contract packages
- Provider implementation が Core runtime helper を必要とする場合のみ AIKernel.Core
- AIKernel.Providers common packages
- pure managed multi-platform .NET libraries
- generic HTTP / JSON / schema / manifest libraries

将来の Provider integration は、public Control surface が必要で、Control logic を再実装しない場合に限り、
AIKernel.Control を package として利用できます。

## Forbidden Dependencies

AIKernel.Providers が参照してはいけないもの:

- AIKernel.Tools
- AIKernel.Wasm
- generic provider package からの AIKernel.Cuda13.0
- generic provider package からの AIKernel.WindowsAI
- NAudio
- SDL
- Microsoft.JSInterop
- Windows SDK / WinRT
- CUDA runtime
- platform-specific implementation packages
- scenario-specific implementation packages

## Package Boundary

C# package boundary は Python wrapper boundary でもあります。将来の
`aikernel-providers` wrapper は C# package surface の薄い wrapper に留め、
fixed native、OS-specific、browser/WASM-specific、vendor-SDK-specific、
scenario-specific implementation を default import surface に混入させてはいけません。

0.1.1.1 validation line では AIKernel.Providers は NuGet-only であり、PyPI package を
build / publish しません。次の公式 v0.1.2 正典シリーズに向けて NuGet + PyPI package の
同期更新を準備します。

## Implementation Ownership

| Category | Owner |
| --- | --- |
| contract | AIKernel.NET |
| core deterministic implementation | AIKernel.Core |
| orchestration | AIKernel.Control |
| provider substrate | AIKernel.Providers |
| provider implementation | AIKernel.Providers または dedicated provider package |
| runtime implementation | AIKernel.Wasm / AIKernel.Cuda13.0 などの dedicated runtime repository |
| scenario implementation | dedicated scenario package / repository |
| tools / CLI | AIKernel.Tools |

## Checklist

- [ ] 他 top-level repository への不正参照がない
- [ ] 固有 SDK / native runtime が generic package に混入していない
- [ ] Python wrapper の default surface が重い依存を要求しない
- [ ] Core が provider implementation に依存していない
- [ ] Control Gate logic をここで再実装していない
- [ ] Providers が Wasm / Tools に依存していない
- [ ] Tools が provider implementation を所有していない
- [ ] Scenario-specific logic が generic providers に混入していない
- [ ] CUDA descriptor は descriptor のままで、CUDA implementation は dedicated backend にある
- [ ] WindowsAI / WinRT implementation が generic Providers に混入していない
- [ ] shared workspace root で `py AIKernel.NET\tools\check_bilingual_xml_docs.py AIKernel.Providers\src` を実行し、public API XML documentation が通る
