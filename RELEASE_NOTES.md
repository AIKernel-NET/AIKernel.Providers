# Release Notes

## 0.1.1.1

**June 2026 - Local development alignment.**
**2026年6月--ローカル開発ラインの整合。**

AIKernel.Providers now follows the AIKernel.Core and AIKernel.Control 0.1.1.1
development policy.

### Changed

- Set the Providers package family to `0.1.1.1`.
- Add local development package versioning through
  `0.1.1.1-dev{build-number}`.
- Resolve AIKernel.NET contract packages from `0.1.1.1`.
- Resolve AIKernel.Core from the local `0.1.1.1-dev1` package family.
- Reserve AIKernel.Control local references through `0.1.1.1-dev4` for future
  provider integrations that need the Control surface.
- Disable PyPI publishing for this update line. Providers 0.1.1.1 is
  NuGet-only; Python wrapper materials remain reference-only.

### Added

- Add `AIKernel.Providers.Substrate` for forward-compatible manifest loading,
  validation, registry, deterministic routing, missing-provider results,
  duplicate diagnostics, fallback policy, diagnostics, evidence references,
  CLI hints, and loose extension merge rules.
  Manifest loader / validator interfaces, dependency descriptors,
  compatibility descriptors, and deterministic resolution requests support
  host-side substitution.
- Add `AIKernel.Providers.Council` for Logos / Ethos / Pathos semantic provider
  envelopes. Council providers emit semantic material and diagnostics only.
  `ProviderSemanticResult.Dimensions` defines stable minimum keys for each
  council and remains outside `GateInput`.
- Add `AIKernel.Providers.Audio` as a pure managed audio substrate with
  `AudioFormat`, `AudioFrame`, format validation, playback base, recording
  base, `AudioRecordFrame`, diagnostics, and routing helpers.
- Add `AIKernel.Providers.Compute` with `ComputeBufferRef`, standardized dtype
  strings, shape / stride string metadata, `HashMetadata`, compute entry point
  descriptors, and compute availability reasons.
- Extend `CudaComputeProvider` with descriptor-driven backend metadata,
  `NativeModuleDescriptor`, `CudaBackendDescriptor`, and structured backend
  resolution. Recognized operations fail closed with `CUDA_BACKEND_NOT_BOUND`
  until a dedicated backend is installed and bound.
- Add dependency guard tests for forbidden package and project references.

### Validation

- Repository configuration is prepared to restore from `../artifacts/local-packages`
  before falling back to nuget.org.
- New substrate, council, audio, compute, CUDA descriptor, and dependency
  boundary tests cover deterministic routing and provider boundary rules.

## 0.1.1

**June 10th, 2026 - Converging provider capabilities.**
**2026年6月10日--プロバイダ能力を収束する。**

Converging provider capabilities: descriptors, manifests, and invokers
stabilize across the 0.1.1 capability graph. プロバイダ能力の収束--Descriptor・
Manifest・Invoker が 0.1.1 能力グラフ全体で安定化する。

Initial public release of AIKernel.Providers, the official extension provider
workspace for AIKernel.NET.

This release establishes the repository boundary for provider-specific logic:
Core keeps runtime contracts and OS-level providers, Tools keeps
instrumentation, and AIKernel.Providers owns official external provider
drivers.

### Added

- Add `ChatOpenAIProvider` for OpenAI-compatible ChatCompletion, Embedding, and
  Moderation capability manifests.
- Add `ChatHistoryProvider` for deterministic chat history records.
- Add `CudaComputeProvider` for abstracting native CUDA compute modules as
  AIKernel providers.
- Add `DynamicPipelineCompilerProvider` for dynamic semantic pipeline compiler
  installation and invocation.
- Add `LocalLlmProvider` for local LLM runtimes such as Ollama, llama.cpp, and
  vLLM.
- Add `AIKernel.Providers.MicrosoftAI`, a Microsoft.Extensions.AI based
  provider implementation moved from AIKernel.Core into AIKernel.Providers
  management.
- Add provider manifest JSON files for dynamically loadable providers.
- Add `aikernel-providers` Python wrapper package with bundled managed
  assemblies and manifest files.

### Changed

- Move provider-specific capability logic out of AIKernel.Tools so Tools remains
  a pure instrumentation layer.
- Move MicrosoftAI provider ownership out of AIKernel.Core so Core no longer
  manages external provider implementation packaging.
- Align provider packages on the `0.1.1` public release semantics. Development
  build changes are folded into this public release note instead of being listed
  as separate package history entries.

### Validation

- Provider projects build as `net10.0` packages.
- Provider test suites validate contract descriptors, manifests, dependency
  direction, and Python wrapper assembly discovery.
- Generated XML documentation contains bilingual `[EN]` and `[JA]` summaries for
  the public package reference surface.
