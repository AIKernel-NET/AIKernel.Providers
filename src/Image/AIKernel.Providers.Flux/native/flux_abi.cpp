#include "flux_abi.h"
#include "stable-diffusion.h"
#ifdef AK_FLUX_CUDA
#include "flux_cuda_context.h"
#endif

#include <atomic>
#include <cstdio>
#include <cstring>
#include <exception>
#include <memory>
#include <mutex>
#include <new>

struct ak_flux_job {
    std::atomic<bool> cancelled{false};
    std::mutex context_mutex;
    sd_ctx_t* context = nullptr;
};

namespace {
// The loader runs on worker threads. Serialize requests while one callback is
// registered in this private copy of the backend, including context teardown.
std::mutex execution;
struct reader_scope {
    ak_flux_reader reader;
    void* state;
#ifdef AK_FLUX_CUDA
    ak_flux_reader device_reader;
    const char* backend;
    std::atomic<uint64_t> device_reads{0};
#endif
    std::atomic<uint64_t> successful_reads{0};
};
std::atomic<reader_scope*> active_reader{nullptr};

struct context_lifetime {
    ak_flux_job& job;
    sd_ctx_t* context;
    ~context_lifetime() {
        std::lock_guard lock(job.context_mutex);
        job.context = nullptr;
        sd_ctx_free(context);
    }
};

int32_t fail(const ak_flux_job& job, char* error, size_t error_size, const char* fallback) {
    const char* diagnostic = sd_get_last_error();
    if (error != nullptr && error_size != 0)
        std::snprintf(error, error_size, "%s", diagnostic != nullptr && diagnostic[0] != '\0' ? diagnostic : fallback);
    return job.cancelled ? AK_FLUX_CANCELLED : AK_FLUX_FAILED;
}

bool dimension_valid(uint32_t value) { return value >= 128 && value <= 1024 && value % 16 == 0; }
}

// Used by the pinned loader translation unit. Registered reads are mandatory.
bool ak_flux_read_weight(const char* path, uint64_t offset, void* destination, size_t size) {
    auto* scope = active_reader.load(std::memory_order_acquire);
    if (scope == nullptr || scope->reader(scope->state, path, offset, destination, size) != 0)
        return false;
    scope->successful_reads.fetch_add(1, std::memory_order_relaxed);
    return true;
}

#ifdef AK_FLUX_CUDA
int ak_flux_read_cuda_weight(const char* buffer_name, const char* path, uint64_t offset, void* destination, size_t size) {
    auto* scope = active_reader.load(std::memory_order_acquire);
    if (scope == nullptr || scope->device_reader == nullptr || buffer_name == nullptr ||
        std::strcmp(buffer_name, scope->backend) != 0)
        return 0;
    if (scope->device_reader(scope->state, path, offset, destination, size) != 0)
        return -1;
    scope->device_reads.fetch_add(1, std::memory_order_relaxed);
    return 1;
}
#endif

uint32_t AK_FLUX_CALL ak_flux_abi_version() { return 1; }

ak_flux_job* AK_FLUX_CALL ak_flux_job_create() { return new (std::nothrow) ak_flux_job; }

void AK_FLUX_CALL ak_flux_job_cancel(ak_flux_job* job) {
    if (job == nullptr) return;
    job->cancelled = true;
    std::lock_guard lock(job->context_mutex);
    if (job->context != nullptr)
        sd_cancel_generation(job->context, SD_CANCEL_ALL);
}

void AK_FLUX_CALL ak_flux_job_free(ak_flux_job* job) { delete job; }

static int32_t edit(ak_flux_job* job, const ak_flux_request* req,
    ak_flux_reader reader, void* state, uint8_t* output, size_t output_size, char* error, size_t error_size
#ifdef AK_FLUX_CUDA
    , ak_flux_reader device_reader = nullptr, int32_t ordinal = 0
#endif
    ) {
    if (error != nullptr && error_size > 0) error[0] = '\0';
    if (job == nullptr || req == nullptr || req->struct_size != sizeof(ak_flux_request) || reader == nullptr ||
        output == nullptr || req->reference_rgb == nullptr || req->prompt == nullptr || req->prompt[0] == '\0' ||
        req->diffusion_path == nullptr || req->text_encoder_path == nullptr || req->vae_path == nullptr ||
        !dimension_valid(req->width) || !dimension_valid(req->height) || req->seed < 0 ||
        req->cpu_threads < 1 || req->cpu_threads > 16)
        return AK_FLUX_INVALID;

    const size_t pixels = static_cast<size_t>(req->width) * req->height * 3;
    if (req->reference_size != pixels || output_size != pixels)
        return AK_FLUX_INVALID;

    try {
        std::lock_guard lock(execution);
        if (job->cancelled) return AK_FLUX_CANCELLED;
        const char* backend = "cpu";
#ifdef AK_FLUX_CUDA
        char runtime_name[24];
        char buffer_name[24];
        if (device_reader != nullptr) {
            std::snprintf(runtime_name, sizeof(runtime_name), "cuda%d", ordinal);
            std::snprintf(buffer_name, sizeof(buffer_name), "CUDA%d", ordinal);
            backend = runtime_name;
        }
        reader_scope binding{reader, state, device_reader, buffer_name};
#else
        reader_scope binding{reader, state};
#endif
        active_reader.store(&binding, std::memory_order_release);
        sd_set_progress_callback([](int, int, float, void*) {}, nullptr);
        struct reset_reader {
            ~reset_reader() {
                active_reader.store(nullptr, std::memory_order_release);
                sd_set_progress_callback(nullptr, nullptr);
            }
        } reset;

        std::unique_ptr<sd_opt_t, decltype(&sd_opt_free)> load(sd_ctx_opt_create(), sd_opt_free);
        if (!load || sd_opt_set_string(load.get(), "diffusion_model_path", req->diffusion_path) != SD_STATUS_OK ||
            sd_opt_set_string(load.get(), "llm_path", req->text_encoder_path) != SD_STATUS_OK ||
            sd_opt_set_string(load.get(), "vae_path", req->vae_path) != SD_STATUS_OK ||
            sd_opt_set_int(load.get(), "n_threads", req->cpu_threads) != SD_STATUS_OK ||
            sd_opt_set_string(load.get(), "backend", backend) != SD_STATUS_OK ||
            sd_opt_set_string(load.get(), "params_backend", "disk") != SD_STATUS_OK ||
            sd_opt_set_bool(load.get(), "enable_mmap", false) != SD_STATUS_OK ||
            sd_opt_set_bool(load.get(), "eager_load", false) != SD_STATUS_OK ||
            sd_opt_set_bool(load.get(), "disable_prefetch", true) != SD_STATUS_OK)
            return fail(*job, error, error_size, "Could not configure FLUX checkpoints.");

        sd_ctx_t* context = nullptr;
        if (sd_ctx_create(load.get(), &context) != SD_STATUS_OK || context == nullptr)
            return fail(*job, error, error_size, "Could not create the FLUX context.");
        context_lifetime lifetime{*job, context};
        {
            std::lock_guard context_lock(job->context_mutex);
            job->context = context;
        }
        if (job->cancelled) return AK_FLUX_CANCELLED;

        std::unique_ptr<sd_opt_t, decltype(&sd_opt_free)> edit(sd_img_gen_opt_create(), sd_opt_free);
        const int64_t shape[] = {3, req->width, req->height};
        std::unique_ptr<sd_tensor_t, decltype(&sd_tensor_free)> reference(
            sd_tensor_create(SD_TENSOR_U8, 3, shape, req->reference_rgb, pixels), sd_tensor_free);
        const sd_tensor_t* references[] = {reference.get()};
        if (!edit || !reference || sd_opt_set_tensors(edit.get(), "ref_images", references, 1) != SD_STATUS_OK ||
            sd_opt_set_string(edit.get(), "prompt", req->prompt) != SD_STATUS_OK ||
            sd_opt_set_int(edit.get(), "seed", req->seed) != SD_STATUS_OK ||
            sd_opt_set_int(edit.get(), "width", req->width) != SD_STATUS_OK ||
            sd_opt_set_int(edit.get(), "height", req->height) != SD_STATUS_OK ||
            sd_opt_set_int(edit.get(), "sample.steps", 4) != SD_STATUS_OK ||
            sd_opt_set_float(edit.get(), "sample.guidance.txt_cfg", 1) != SD_STATUS_OK ||
            sd_opt_set_string(edit.get(), "sample.method", "euler") != SD_STATUS_OK)
            return fail(*job, error, error_size, "Could not configure the FLUX image edit.");

        sd_result_t* generated = nullptr;
        auto status = sd_generate_image(context, edit.get(), &generated);
        std::unique_ptr<sd_result_t, decltype(&sd_result_free)> result(generated, sd_result_free);
        auto host_reads = binding.successful_reads.load(std::memory_order_relaxed);
        if (status != SD_STATUS_OK || !result || sd_result_get_image_count(result.get()) != 1)
            return fail(*job, error, error_size, "FLUX did not produce an image using the weight reader.");
#ifdef AK_FLUX_CUDA
        if (device_reader != nullptr) {
            if (binding.device_reads.load(std::memory_order_relaxed) == 0)
                return fail(*job, error, error_size, "FLUX did not load any weights through the CUDA device pages.");
        } else
#endif
        if (host_reads == 0)
            return fail(*job, error, error_size, "FLUX did not load any weights through the host pages.");

        const sd_tensor_t* image = sd_result_get_image(result.get(), 0);
        if (image == nullptr || sd_tensor_type(image) != SD_TENSOR_U8 || sd_tensor_rank(image) != 3 ||
            sd_tensor_dim(image, 0) != 3 || sd_tensor_dim(image, 1) != req->width ||
            sd_tensor_dim(image, 2) != req->height || sd_tensor_byte_size(image) != pixels ||
            sd_tensor_data(image) == nullptr)
            return fail(*job, error, error_size, "FLUX returned unexpected RGB dimensions.");

        if (job->cancelled) return AK_FLUX_CANCELLED;
        std::memcpy(output, sd_tensor_data(image), pixels);
        return AK_FLUX_OK;
    } catch (const std::exception& ex) {
        if (error != nullptr && error_size != 0) std::snprintf(error, error_size, "%s", ex.what());
        return job->cancelled ? AK_FLUX_CANCELLED : AK_FLUX_FAILED;
    } catch (...) {
        return fail(*job, error, error_size, "Unknown failure in the FLUX native runtime.");
    }
}

int32_t AK_FLUX_CALL ak_flux_edit(ak_flux_job* job, const ak_flux_request* req,
    ak_flux_reader reader, void* state, uint8_t* output, size_t output_size, char* error, size_t error_size) {
    return edit(job, req, reader, state, output, output_size, error, error_size);
}

#ifdef AK_FLUX_CUDA
uint32_t AK_FLUX_CALL ak_flux_cuda_abi_version() { return 1; }

int32_t AK_FLUX_CALL ak_flux_cuda_edit(ak_flux_job* job, const ak_flux_request* req,
    ak_flux_reader host_reader, ak_flux_reader device_reader, void* state, const ak_flux_cuda_device* device,
    uint8_t* output, size_t output_size, char* error, size_t error_size) {
    if (device_reader == nullptr || device == nullptr)
        return AK_FLUX_INVALID;
    ak_flux_cuda_context_scope scope(device->context);
    if (scope.result != CUDA_SUCCESS) {
        if (error != nullptr && error_size != 0)
            std::snprintf(error, error_size, "Could not enter the selected CUDA context (%d).", scope.result);
        return AK_FLUX_FAILED;
    }
    return edit(job, req, host_reader, state, output, output_size, error, error_size, device_reader, device->ordinal);
}
#endif
