#pragma once

#include <stddef.h>
#include <stdint.h>

#ifdef _WIN32
#define AK_FLUX_API __declspec(dllexport)
#define AK_FLUX_CALL __cdecl
#else
#define AK_FLUX_API __attribute__((visibility("default")))
#define AK_FLUX_CALL
#endif

#ifdef __cplusplus
extern "C" {
#endif

typedef struct ak_flux_job ak_flux_job;
typedef struct ak_flux_cuda_device ak_flux_cuda_device;

// Copy exactly size bytes and return 0 on success. The destination is borrowed
// only for this callback. Calls may occur on native worker threads.
typedef int32_t (AK_FLUX_CALL *ak_flux_reader)(void* state, const char* path,
                                                uint64_t offset, void* destination, size_t size);

typedef struct ak_flux_request {
    uint32_t struct_size;
    uint32_t width;
    uint32_t height;
    uint32_t cpu_threads;
    int64_t seed;
    const char* diffusion_path;
    const char* text_encoder_path;
    const char* vae_path;
    const char* prompt;
    const uint8_t* reference_rgb;
    size_t reference_size;
} ak_flux_request;

enum { AK_FLUX_OK = 0, AK_FLUX_INVALID = 1, AK_FLUX_FAILED = 2, AK_FLUX_CANCELLED = 3 };

AK_FLUX_API uint32_t AK_FLUX_CALL ak_flux_abi_version(void);
AK_FLUX_API ak_flux_job* AK_FLUX_CALL ak_flux_job_create(void);
AK_FLUX_API void AK_FLUX_CALL ak_flux_job_cancel(ak_flux_job* job);
AK_FLUX_API void AK_FLUX_CALL ak_flux_job_free(ak_flux_job* job);
AK_FLUX_API int32_t AK_FLUX_CALL ak_flux_edit(ak_flux_job* job, const ak_flux_request* request,
    ak_flux_reader reader, void* state, uint8_t* output, size_t output_size, char* error, size_t error_size);

#ifdef AK_FLUX_CUDA
AK_FLUX_API uint32_t AK_FLUX_CALL ak_flux_cuda_abi_version(void);
AK_FLUX_API int32_t AK_FLUX_CALL ak_flux_cuda_edit(ak_flux_job* job, const ak_flux_request* request,
    ak_flux_reader host_reader, ak_flux_reader device_reader, void* state, const ak_flux_cuda_device* device,
    uint8_t* output, size_t output_size, char* error, size_t error_size);
#endif

#ifdef __cplusplus
}
#endif
