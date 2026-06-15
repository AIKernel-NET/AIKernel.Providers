# Perception Provider Substrate

`AIKernel.Providers.Perception` は、perception / spatial cognition Provider discovery のための provider-substrate contract を保持します。runtime 固有、native、browser、scenario 固有の implementation code は含めません。

## 責務

- provider-neutral な perception capability を記述する。
- frame、auditory、spatial Provider 向け deterministic routing policy を作成する。
- manifest-driven provider discovery を維持する。
- confidence、risk、diagnostics を downstream adapter 向け semantic material として保持する。

## v0.1.2 への整理

この package の interface / DTO は、次回の AIKernel.NET 正典 Interface 更新で昇格候補になります。それまでは小さく、adapter-friendly で、scenario-specific semantics を持たない状態に保ちます。
