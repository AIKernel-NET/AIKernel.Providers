# AIKernel.Providers Release Notes

[日本語](RELEASE_NOTES-ja.md)

## 0.1.3 (Unreleased)

- Align the official Provider packages with AIKernel.NET, Core, and Control 0.1.3. Keep endpoint, driver, and optional native-runtime concerns outside Core.
- Include the Compute provider in `aikernel-providers` bundle verification: check that the matching DLL is loaded and that the provider can be registered. Native CUDA execution remains an opt-in capability.

## 0.1.2

**June 16th, 2026 - Provider substrate package line.**

AIKernel.Providers 0.1.2 publishes the provider substrate and runtime-configurable provider family aligned with AIKernel.NET and AIKernel.Core 0.1.2.

- Publish provider substrate, standard, audio, compute, perception, council, chat, local LLM, Microsoft AI, CUDA descriptor, and dynamic pipeline packages.
- Keep Providers as capability adapters rather than SDK wrappers.
- Preserve deterministic router and manifest behavior.
- Publish synchronized Python wrapper materials through the 0.1.2 release flow.
