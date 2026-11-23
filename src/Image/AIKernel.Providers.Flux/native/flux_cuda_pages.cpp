#include "flux_cuda_pages.h"
#include "flux_cuda_context.h"
#include <new>

struct ak_flux_cuda_page {
    const ak_flux_cuda_device* device;
    CUdeviceptr address;
    size_t size;
};

int32_t AK_FLUX_CALL ak_flux_cuda_device_open(int32_t ordinal, ak_flux_cuda_device** device) {
    if (!device || ordinal < 0) return CUDA_ERROR_INVALID_VALUE;
    *device = nullptr;
    auto status = cuInit(0);
    if (status != CUDA_SUCCESS) return status;
    CUdevice id;
    status = cuDeviceGet(&id, ordinal);
    if (status != CUDA_SUCCESS) return status;
    auto* handle = new (std::nothrow) ak_flux_cuda_device{ordinal, id, nullptr};
    if (!handle) return CUDA_ERROR_OUT_OF_MEMORY;
    status = cuDevicePrimaryCtxRetain(&handle->context, id);
    if (status != CUDA_SUCCESS) {
        delete handle;
        return status;
    }
    *device = handle;
    return CUDA_SUCCESS;
}

int32_t AK_FLUX_CALL ak_flux_cuda_device_close(ak_flux_cuda_device* device) {
    if (!device) return CUDA_SUCCESS;
    auto status = cuDevicePrimaryCtxRelease(device->id);
    if (status == CUDA_SUCCESS) delete device;
    return status;
}

int32_t AK_FLUX_CALL ak_flux_cuda_page_upload(ak_flux_cuda_device* device,
    const void* source, size_t size, ak_flux_cuda_page** page) {
    if (!device || !source || !size || !page) return CUDA_ERROR_INVALID_VALUE;
    *page = nullptr;
    ak_flux_cuda_context_scope scope(device->context);
    if (scope.result != CUDA_SUCCESS) return scope.result;
    auto* block = new (std::nothrow) ak_flux_cuda_page{device, 0, size};
    if (!block) return CUDA_ERROR_OUT_OF_MEMORY;
    auto status = cuMemAlloc(&block->address, size);
    if (status == CUDA_SUCCESS) status = cuMemcpyHtoD(block->address, source, size);
    if (status == CUDA_SUCCESS) status = cuStreamSynchronize(nullptr);
    if (status != CUDA_SUCCESS) {
        if (block->address) cuMemFree(block->address);
        delete block;
        return status;
    }
    *page = block;
    return CUDA_SUCCESS;
}

int32_t AK_FLUX_CALL ak_flux_cuda_page_download(const ak_flux_cuda_page* page, void* destination, size_t size) {
    if (!page || !destination || size != page->size) return CUDA_ERROR_INVALID_VALUE;
    ak_flux_cuda_context_scope scope(page->device->context);
    if (scope.result != CUDA_SUCCESS) return scope.result;
    return cuMemcpyDtoH(destination, page->address, size);
}

int32_t AK_FLUX_CALL ak_flux_cuda_page_free(ak_flux_cuda_page* page) {
    if (!page) return CUDA_SUCCESS;
    ak_flux_cuda_context_scope scope(page->device->context);
    if (scope.result != CUDA_SUCCESS) return scope.result;
    auto status = cuMemFree(page->address);
    if (status == CUDA_SUCCESS) delete page;
    return status;
}

int32_t AK_FLUX_CALL ak_flux_cuda_page_copy(const ak_flux_cuda_page* page, size_t offset, void* destination, size_t size) {
    if (!page || !destination || !size || offset > page->size || size > page->size - offset)
        return CUDA_ERROR_INVALID_VALUE;
    ak_flux_cuda_context_scope scope(page->device->context);
    if (scope.result != CUDA_SUCCESS) return scope.result;
    const auto target = reinterpret_cast<CUdeviceptr>(destination);
    CUcontext owner = nullptr;
    auto status = cuPointerGetAttribute(&owner, CU_POINTER_ATTRIBUTE_CONTEXT, target);
    if (status != CUDA_SUCCESS) return status;
    if (owner != page->device->context) return CUDA_ERROR_INVALID_CONTEXT;
    CUdeviceptr base = 0;
    size_t capacity = 0;
    status = cuMemGetAddressRange(&base, &capacity, target);
    if (status != CUDA_SUCCESS) return status;
    if (target < base || target - base > capacity || size > capacity - (target - base))
        return CUDA_ERROR_INVALID_VALUE;
    status = cuMemcpyDtoDAsync(target, page->address + offset, size, CU_STREAM_PER_THREAD);
    return status == CUDA_SUCCESS ? cuStreamSynchronize(CU_STREAM_PER_THREAD) : status;
}
