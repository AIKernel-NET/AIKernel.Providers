# DynamicPipelineCompilerProvider

[日本語](README-ja.md)

DynamicPipelineCompilerProvider exposes dynamic semantic pipeline compilation as
an AIKernel provider capability.

## Role

The provider is designed for CLI-driven provider installation and loading. A
host can install and load it as an external compiler provider instead of
embedding dynamic compiler behavior into Core or Tools.

Example intended CLI shape:

```bash
aik install provider dynamic-pipeline
```

## Capability Surface

The provider exposes:

- `pipeline.compile`
- `pipeline.execute`
- `pipeline.validate`

Default provider identity:

- Provider id: `providers.dynamic-pipeline`
- Manifest id: `dynamic-pipeline`
- Manifest file: `dynamic-pipeline.provider.json`

## Manifest Metadata

The manifest and settings record:

- DSL schema version
- optional DSL schema URI

These values let hosts validate compatibility before loading or invoking the
compiler provider.

## Dependency Boundary

DynamicPipelineCompilerProvider describes and hosts external compiler behavior.
Core keeps DSL contracts and runtime semantics. Tools keeps instrumentation.
