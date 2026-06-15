# Perception Provider Substrate

`AIKernel.Providers.Perception` contains provider-substrate contracts for perception and spatial cognition provider discovery. It deliberately does not contain runtime-specific, native, browser, or scenario-specific implementation code.

## Responsibilities

- Describe provider-neutral perception capabilities.
- Create deterministic routing policies for frame, auditory, and spatial providers.
- Preserve manifest-driven provider discovery.
- Keep confidence, risk, and diagnostics as semantic material for downstream adapters.

## v0.1.2 Alignment

The interfaces and DTOs in this package are promotion candidates for the next AIKernel.NET canonical interface update. Until then, they should stay small, adapter-friendly, and free from scenario-specific semantics.
