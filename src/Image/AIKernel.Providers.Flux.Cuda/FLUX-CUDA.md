# AIKernel FLUX.2 CUDA image editing provider

`FluxCudaImageProvider` edits RGB24 reference images with FLUX.2 [klein] 4B. The package targets .NET 10 and Windows x64. It uses the same `IImagePromptEditProvider` contract and [registered checkpoints](https://github.com/AIKernel-NET/AIKernel.Providers/blob/9fa32e5/src/Image/AIKernel.Providers.Flux/FLUX.md#register-the-local-model-files) as the CPU package.

```csharp
using AIKernel.Contracts.Images;
using AIKernel.Providers.Flux;

using var runtime = new FluxCudaImageProvider(modelDirectory, privateSsdDirectory,
    weightCacheMiB: 256, deviceOrdinal: 0, deviceCacheMiB: 64);
IImagePromptEditProvider editor = runtime;
var edited = await editor.EditAsync(rgb24, width, height,
    "Make the jacket blue and preserve the composition.", seed: 42, cancellationToken);
```

Use an AVX2-capable host with the Microsoft Visual C++ Redistributable, an NVIDIA driver compatible with CUDA 13.2, and the CUDA 13.2 runtime and cuBLAS DLLs on `PATH`. The package contains the FLUX bridge. Install the model files and the NVIDIA runtime separately. Device ordinals follow CUDA device visibility in the application process.

AIKernel bounds the RAM and GPU checkpoint caches separately. For compatible tensor layouts, the native loader borrows each resident GPU page and copies into the engine's CUDA tensor allocation. Tensors that require conversion use the host reader before upload. The GPU page budget covers the checkpoint cache. Tensor staging, activations, and decoder workspace also need physical memory. A failed CUDA edit returns an error to the application.

`LastWeightStatistics` reports resident cache bytes, logical SSD checkpoint reads, device page-ins, and the number of tensor reads delivered to host and device memory. The logical SSD reads count requests to the checkpoint source and may be served by the OS file cache.

## Build

Use the pinned engine and submodule revisions from the [CPU build guide](https://github.com/AIKernel-NET/AIKernel.Providers/blob/9fa32e5/src/Image/AIKernel.Providers.Flux/FLUX.md#build-the-native-dependency). Set `$engine` to that checkout and `$cudaToolkit` to the CUDA 13.2 toolkit directory. Run these commands in a Visual Studio x64 developer terminal from the `AIKernel.Providers.Flux.Cuda` directory.

```powershell
cmake -S ../AIKernel.Providers.Flux/native -B native-build-cuda -G Ninja `
  -DCMAKE_BUILD_TYPE=Release -DAIKERNEL_FLUX_CUDA=ON `
  '-DCMAKE_CUDA_ARCHITECTURES=75-real;86-real;89-real;89-virtual' `
  "-DAIKERNEL_FLUX_ENGINE=$engine" "-DCUDAToolkit_ROOT=$cudaToolkit" `
  "-DCMAKE_CUDA_COMPILER=$cudaToolkit/bin/nvcc.exe"
cmake --build native-build-cuda --parallel 4
dotnet pack -c Release -p:UseAIKernelContractSource=false `
  "-p:FluxCudaNativeLibrary=$((Resolve-Path native-build-cuda/aikernel_flux_cuda.dll).Path)"
```

This build emits native code for SM 7.5, 8.6 and 8.9, plus PTX for compute capability 8.9. Adjust the architecture list for other boards. The CMake build leaves the upstream checkout untouched. It checks both revisions and generates a tensor-loader translation unit with the ABI callbacks.
