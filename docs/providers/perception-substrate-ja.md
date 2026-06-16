# Perception Provider Substrate

`AIKernel.Providers.Perception` は、perception / spatial cognition Provider discovery のための provider-substrate contract を保持します。runtime 固有、native、browser、scenario 固有の implementation code は含めません。

## 責務

- provider-neutral な perception capability を記述する。
- frame、auditory、spatial Provider 向け deterministic routing policy を作成する。
- manifest-driven provider discovery を維持する。
- confidence、risk、diagnostics を downstream adapter 向け semantic material として保持する。
- Sensor OS carrier を provider-neutral に保ち、v0.1.2 contract 抽出へ備える。

## Sensor OS concept mapping

`AIKernel.Providers.Perception` は、現在の Sensor OS 抽出候補における
provider-neutral な置き場所です。ここが所有するのは substrate shape のみであり、
browser execution、Doom mapping、Gate decision は所有しません。

| Sensor | Concept |
| --- | --- |
| `visual`, `audio`, `health` | `Aisthesis` |
| `motor`, `movement` | `Kinesis` |
| `compass`, `spatial` | `Phantasia` |

リポジトリ横断の判断では、
[リポジトリ横断開発者ガイド v0.1.1.1](https://github.com/AIKernel-NET/AIKernel.NET/blob/main/docs/development/cross-repository-developer-guide-v0.1.1.1-ja.md)
を参照してください。

## Resident Perception Algorithm Library

`AIKernel.Providers.Perception` は、pure managed な resident perception
algorithm fallback surface も所有します。これらは `Aisthesis` の raw data を
`Phantasia` の representation carrier へ変換する汎用ライブラリ関数です。
Doom 固有 logic ではなく、CTG、Gate、Council、intent、action value は生成しません。

提供するアルゴリズム:

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

`IResidentPerceptionAlgorithmKernel` は CPU fallback と将来の accelerator-backed
implementation を差し替える共通抽象です。この package は pure managed に保ち、
WebGPU dispatch descriptor は `AIKernel.Wasm.Perception` に置きます。

## v0.1.2 への整理

この package の interface / DTO は、次回の AIKernel.NET 正典 Interface 更新で昇格候補になります。それまでは小さく、adapter-friendly で、scenario-specific semantics を持たない状態に保ちます。
