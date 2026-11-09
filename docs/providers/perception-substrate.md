# Perception Provider Substrate

`AIKernel.Providers.Perception` contains provider-substrate contracts for perception and spatial cognition provider discovery. It deliberately does not contain runtime-specific, native, browser, or scenario-specific implementation code.

## Responsibilities

- Describe provider-neutral perception capabilities.
- Create deterministic routing policies for frame, auditory, and spatial providers.
- Preserve manifest-driven provider discovery.
- Keep confidence, risk, and diagnostics as semantic material for downstream adapters.
- Keep Sensor OS carriers provider-neutral and ready for v0.1.3 contract
  extraction.

## Sensor OS Concept Mapping

`AIKernel.Providers.Perception` is the provider-neutral home for the current
Sensor OS extraction candidates. It owns substrate shapes only; it does not own
browser execution, Doom mapping, or Gate decisions.

| Sensor | Concept |
| --- | --- |
| `visual`, `audio`, `health` | `Aisthesis` |
| `motor`, `movement` | `Kinesis` |
| `compass`, `spatial` | `Phantasia` |

For cross-repository decisions, use the
[Cross-Repository Developer Guide v0.1.1.1](https://github.com/AIKernel-NET/AIKernel.NET/blob/main/docs/development/cross-repository-developer-guide-v0.1.1.1.md).

## Resident Perception Algorithm Library

`AIKernel.Providers.Perception` also owns the pure managed resident perception
algorithm fallback surface. These algorithms convert `Aisthesis` raw data into
`Phantasia` representation carriers. They are generic library functions, not
Doom-specific logic, and they do not create CTG, Gate, Council, intent, or
action values.

Provided algorithms:

- Semantic palette quantization
- Temporal difference filtering
- Laplacian edge detection
- Binaural direction quantization
- Frequency band energy splitting
- RGB to HSV threshold masking
- Max-pooling downsampling
- Morphology dilation / erosion
- Dense optical flow
- Audio spectrum / FFT-style carrier generation
- Exponential moving average
- Leaky integrator
- Grid-based spatial hashing
- Scalar Kalman filter update

`IResidentPerceptionAlgorithmKernel` is the common abstraction for CPU fallback
and future accelerator-backed implementations. Providers must remain pure
managed here; WebGPU dispatch descriptors live in `AIKernel.Wasm.Perception`.

## v0.1.3 Alignment

The interfaces and DTOs in this package are promotion candidates for the next AIKernel.NET canonical interface update. Until then, they should stay small, adapter-friendly, and free from scenario-specific semantics.
