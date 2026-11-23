#pragma once
#include <cuda.h>

struct ak_flux_cuda_device {
    int ordinal;
    CUdevice id;
    CUcontext context;
};

// Restore the caller's CUDA context even on a failed operation.
class ak_flux_cuda_context_scope {
public:
    explicit ak_flux_cuda_context_scope(CUcontext context) : result(cuCtxPushCurrent(context)) {}
    ak_flux_cuda_context_scope(const ak_flux_cuda_context_scope&) = delete;
    ak_flux_cuda_context_scope& operator=(const ak_flux_cuda_context_scope&) = delete;

    ~ak_flux_cuda_context_scope() {
        if (result == CUDA_SUCCESS) {
            CUcontext popped = nullptr;
            cuCtxPopCurrent(&popped);
        }
    }

    const CUresult result;
};
