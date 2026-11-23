# AIKernel FLUX.2 image editing provider

This independent provider edits an RGB24 reference image with a text instruction using FLUX.2 [klein] 4B. It targets .NET 10 with Windows x64 CPU execution. The original image provider can be used in the same application.

```sh
dotnet add package AIKernel.Providers.Flux --version 0.1.5
```

```csharp
using AIKernel.Contracts.Images;
using AIKernel.Providers.Flux;

using var runtime = new FluxImageProvider(modelDirectory, privateSsdDirectory, weightCacheMiB: 256);
IImagePromptEditProvider editor = runtime;
ReadOnlyMemory<byte> edited = await editor.EditAsync(referenceRgb24, width, height,
    "Make the jacket blue and keep the original composition.", seed: 42, cancellationToken);
```

Pass consecutive rows of packed red, green, and blue bytes. Both dimensions must be multiples of 16 between 128 and 1024. The provider returns RGB24 of the same size. File codecs, UI, and routing belong to the application. The Windows release requires an AVX2-capable CPU and the Microsoft Visual C++ Redistributable.

## Register the local model files

Keep the following three files in `modelDirectory`. The provider verifies their exact sizes and SHA-256 digests before inference. The weights are provisioned separately from the package.

| Filename | Source |
| --- | --- |
| `flux-2-klein-4b-Q4_0.gguf` | [leejet FLUX.2 klein 4B Q4_0](https://huggingface.co/leejet/FLUX.2-klein-4B-GGUF/tree/3b1f5a9dc3abb32238b053aeb3d823c30afdacbd) |
| `Qwen3-4B-Q4_K_M.gguf` | [Qwen3 4B](https://huggingface.co/Qwen/Qwen3-4B-GGUF/tree/bc640142c66e1fdd12af0bd68f40445458f3869b) |
| `flux2-vae.safetensors` | Save the [4B VAE `diffusion_pytorch_model.safetensors`](https://huggingface.co/black-forest-labs/FLUX.2-klein-4B/blob/e7b7dc27f91deacad38e78976d1f2b499d76a294/vae/diffusion_pytorch_model.safetensors) under this name. |

All three pinned weight sources identify their license as Apache-2.0. The accepted checkpoint is the distilled 4B variant.

## ABI and memory

The provider calls ABI v1 of `aikernel_flux.dll`. The native tensor loader requests bytes through a callback. AIKernel `VirtualMemorySpace` owns the RAM cache, with checkpoint files on SSD as immutable backing. `LastWeightStatistics` reports resident cache bytes, successful native read requests, and logical reads from the checkpoint source. The budget applies to checkpoint pages. Native tensor staging, activations and decoder workspace have their own allocations. See the [CUDA provider build guide](../AIKernel.Providers.Flux.Cuda/FLUX-CUDA.md) for the optional GPU implementation.

## Build the native dependency

Use a Visual Studio x64 developer terminal with CMake and Ninja. Initialize the `ggml` submodule at the engine commit.

```powershell
git clone https://github.com/leejet/stable-diffusion.cpp.git
git -C stable-diffusion.cpp checkout 0d71f963af9c94941f4a67e8ea145935651dd756
git -C stable-diffusion.cpp submodule update --init ggml
cmake -S native -B native-build -G Ninja -DCMAKE_BUILD_TYPE=Release -DAIKERNEL_FLUX_ENGINE="$((Resolve-Path stable-diffusion.cpp).Path)"
cmake --build native-build --parallel 4
dotnet pack AIKernel.Providers.Flux.csproj -c Release -p:FluxNativeLibrary="$((Resolve-Path native-build/aikernel_flux.dll).Path)"
```

The source build checks the engine and ggml revisions. CMake generates a translation unit with the tensor read redirected to the ABI. It leaves the pinned checkout untouched.
