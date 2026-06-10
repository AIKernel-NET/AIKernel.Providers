# CudaComputeProvider

[日本語](README-ja.md)

CudaComputeProvider exposes native CUDA compute modules through the AIKernel
provider boundary.

## Role

The provider is the abstraction layer that lets AIKernel hosts treat native CUDA
modules, including CUDA 13 style native implementations, as capability modules.
It does not implement CUDA kernels itself.

## Capability Surface

The provider exposes:

- `tensor.matmul`
- `tensor.softmax`
- `tensor.conv2d`
- `tensor.layernorm`

Default provider identity:

- Provider id: `providers.cuda`
- Manifest id: `cuda.compute`
- Manifest file: `cuda.provider.json`

## Manifest Metadata

The manifest and settings record:

- device profile
- native entry point
- loader manifest URI
- optional artifact hash

## Dependency Boundary

CudaComputeProvider may describe native ABI metadata, but it must not pull
native execution into Core or Tools. Native implementation packages remain
outside this provider boundary.
