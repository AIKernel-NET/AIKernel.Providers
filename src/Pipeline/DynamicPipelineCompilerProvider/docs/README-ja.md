# DynamicPipelineCompilerProvider

[English](README.md)

DynamicPipelineCompilerProvider は dynamic semantic pipeline compilation を
AIKernel Provider capability として公開します。

## Role

この Provider は CLI-driven provider installation / loading を想定しています。
host は dynamic compiler behavior を Core や Tools に埋め込まず、外部 compiler
Provider として install / load できます。

想定する CLI 形状:

```bash
aik install provider dynamic-pipeline
```

## Capability Surface

Provider は以下を公開します。

- `pipeline.compile`
- `pipeline.execute`
- `pipeline.validate`

default provider identity:

- Provider id: `providers.dynamic-pipeline`
- Manifest id: `dynamic-pipeline`
- Manifest file: `dynamic-pipeline.provider.json`

## Manifest Metadata

manifest と settings は以下を記録します。

- DSL schema version
- optional DSL schema URI

host はこれらの値を使い、compiler Provider の load / invoke 前に compatibility
を検証できます。

## Dependency Boundary

DynamicPipelineCompilerProvider は外部 compiler behavior を記述し host します。
Core は DSL contract と runtime semantics を保持し、Tools は instrumentation を
保持します。
