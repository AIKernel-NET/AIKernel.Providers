# CudaComputeProvider

[English](README.md)

CudaComputeProvider は native CUDA compute module を AIKernel Provider boundary
から公開します。

## Role

この Provider は、CUDA 13 系 native implementation を含む native CUDA module を
AIKernel host が capability module として扱うための abstraction layer です。
CUDA kernel 自体は実装しません。

## Capability Surface

Provider は以下を公開します。

- `tensor.matmul`
- `tensor.softmax`
- `tensor.conv2d`
- `tensor.layernorm`

default provider identity:

- Provider id: `providers.cuda`
- Manifest id: `cuda.compute`
- Manifest file: `cuda.provider.json`

## Manifest Metadata

manifest と settings は以下を記録します。

- device profile
- native entry point
- loader manifest URI
- optional artifact hash

## Dependency Boundary

CudaComputeProvider は native ABI metadata を記述できますが、native execution を
Core や Tools へ取り込んではいけません。native implementation package はこの
Provider boundary の外に残ります。
