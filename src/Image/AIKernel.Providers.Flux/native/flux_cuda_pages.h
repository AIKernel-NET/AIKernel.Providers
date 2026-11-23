#pragma once
#include "flux_abi.h"

#ifdef __cplusplus
extern "C" {
#endif

typedef struct ak_flux_cuda_page ak_flux_cuda_page;

// The device handle retains the CUDA primary context until its pages are freed.
AK_FLUX_API int32_t AK_FLUX_CALL ak_flux_cuda_device_open(int32_t ordinal, ak_flux_cuda_device** device);
AK_FLUX_API int32_t AK_FLUX_CALL ak_flux_cuda_device_close(ak_flux_cuda_device* device);
AK_FLUX_API int32_t AK_FLUX_CALL ak_flux_cuda_page_upload(ak_flux_cuda_device* device,
    const void* source, size_t size, ak_flux_cuda_page** page);
AK_FLUX_API int32_t AK_FLUX_CALL ak_flux_cuda_page_download(const ak_flux_cuda_page* page,
    void* destination, size_t size);
AK_FLUX_API int32_t AK_FLUX_CALL ak_flux_cuda_page_free(ak_flux_cuda_page* page);

// Copies to a borrowed destination allocation in the same primary context.
// The function waits for the copy before the caller can evict the page.
AK_FLUX_API int32_t AK_FLUX_CALL ak_flux_cuda_page_copy(const ak_flux_cuda_page* page,
    size_t offset, void* destination, size_t size);

#ifdef __cplusplus
}
#endif
