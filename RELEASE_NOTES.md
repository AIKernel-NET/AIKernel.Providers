# Release Notes

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
