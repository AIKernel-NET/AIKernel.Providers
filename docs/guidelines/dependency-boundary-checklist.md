# Dependency Boundary Checklist

[日本語](dependency-boundary-checklist-ja.md)

Use this checklist before adding or accepting provider implementations in
AIKernel.Providers.

## Repository Role

AIKernel.Providers owns:

- provider substrate
- provider manifests
- provider registry / router / resolution policy
- runtime-configurable providers
- pure managed multi-platform provider implementations
- descriptor / invoker boundaries for dedicated backend packages
- reference Python wrapper materials for the Providers repository boundary

AIKernel.Providers does not own:

- Core deterministic governance logic
- Control orchestration logic
- Tools CLI / inspection implementation
- browser / WASM runtime implementation
- CUDA native runtime implementation
- Windows SDK / WinRT implementation
- scenario-specific runtime implementations

## Allowed Dependencies

AIKernel.Providers may reference:

- AIKernel.NET contract packages
- AIKernel.Core, only when provider implementation needs Core runtime helpers
- AIKernel.Providers common packages
- pure managed multi-platform .NET libraries
- generic HTTP / JSON / schema / manifest libraries

Future provider integrations may consume AIKernel.Control as a package only when
the integration needs the public Control surface and does not reimplement
Control logic.

## Forbidden Dependencies

AIKernel.Providers must not reference:

- AIKernel.Tools
- AIKernel.Wasm
- AIKernel.Cuda13.0 from generic provider packages
- AIKernel.WindowsAI from generic provider packages
- NAudio
- SDL
- Microsoft.JSInterop
- Windows SDK / WinRT
- CUDA runtime
- platform-specific implementation packages
- scenario-specific implementation packages

## Package Boundary

The C# package boundary is also the Python wrapper boundary. The
`aikernel-providers` wrapper must stay thin over the C# package surface and
must not pull fixed native, OS-specific, browser/WASM-specific,
vendor-SDK-specific, or scenario-specific implementations into the default
import surface.

In the v0.1.3 canonical series, use `0.1.3.dev{buildNumber}` wheels for local
Python validation until stable publication is explicitly opened.

## Implementation Ownership

| Category | Owner |
| --- | --- |
| contract | AIKernel.NET |
| core deterministic implementation | AIKernel.Core |
| orchestration | AIKernel.Control |
| provider substrate | AIKernel.Providers |
| provider implementation | AIKernel.Providers or a dedicated provider package |
| runtime implementation | Dedicated runtime repository such as AIKernel.Wasm or AIKernel.Cuda13.0 |
| scenario implementation | Dedicated scenario package / repository |
| tools / CLI | AIKernel.Tools |

## Checklist

- [ ] No illegal references to other top-level repositories.
- [ ] No fixed SDK or native runtime is mixed into a generic package.
- [ ] The default Python wrapper surface does not require heavy dependencies.
- [ ] Core does not depend on provider implementations.
- [ ] Control Gate logic is not reimplemented here.
- [ ] Providers do not depend on Wasm or Tools.
- [ ] Tools do not own provider implementations.
- [ ] Scenario-specific logic is not mixed into generic providers.
- [ ] CUDA descriptors remain descriptors; CUDA implementation stays in a dedicated backend.
- [ ] WindowsAI / WinRT implementation stays out of generic Providers.
- [ ] Public API XML documentation passes `py AIKernel.NET\tools\check_bilingual_xml_docs.py AIKernel.Providers\src` from the shared workspace root.
