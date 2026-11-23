#include "whisper.h"
#include <cstddef>
#include <cstdint>

#if defined(_WIN32)
#  define AK_EXPORT extern "C" __declspec(dllexport)
#  define AK_CALL __cdecl
#else
#  define AK_EXPORT extern "C" __attribute__((visibility("default")))
#  define AK_CALL
#endif

using read_callback = std::size_t (AK_CALL *)(void *, void *, std::size_t);
using cancel_callback = int (AK_CALL *)(void *);

struct reader_state {
    read_callback reader;
    void * user;
    bool eof;
};

struct cancel_state {
    cancel_callback callback;
    void * user;
};

AK_EXPORT int AK_CALL ak_whisper_abi_version() {
    return 1;
}

AK_EXPORT int AK_CALL ak_whisper_cpu_requirements() {
#if defined(_M_X64) || defined(__x86_64__)
    return AK_REQUIRES_AVX | (AK_REQUIRES_AVX2 << 1) | (AK_REQUIRES_BMI2 << 2) |
        (AK_REQUIRES_FMA << 3) | (AK_REQUIRES_F16C << 4);
#else
    return 0;
#endif
}

AK_EXPORT whisper_context * AK_CALL ak_whisper_open(read_callback reader, void * user) {
    reader_state state{reader, user, false};
    whisper_model_loader loader{
        &state,
        [](void * context, void * buffer, std::size_t length) -> std::size_t {
            auto & stream = *static_cast<reader_state *>(context);
            const auto count = stream.reader(stream.user, buffer, length);
            stream.eof = count < length;
            return count;
        },
        [](void * context) -> bool { return static_cast<reader_state *>(context)->eof; },
        [](void *) {}
    };
    auto parameters = whisper_context_default_params();
    parameters.use_gpu = false;
    return whisper_init_with_params(&loader, parameters);
}

AK_EXPORT void AK_CALL ak_whisper_close(whisper_context * context) {
    whisper_free(context);
}

AK_EXPORT int AK_CALL ak_whisper_transcribe(whisper_context * context, const float * mono,
    int count, const char * language, int threads, cancel_callback cancel, void * user) {
    auto parameters = whisper_full_default_params(WHISPER_SAMPLING_GREEDY);
    if (threads > 0) {
        parameters.n_threads = threads;
    }
    parameters.language = language;
    parameters.translate = false;
    parameters.no_context = true;
    parameters.print_progress = false;
    parameters.print_realtime = false;
    parameters.print_timestamps = false;
    parameters.print_special = false;
    cancel_state state{cancel, user};
    parameters.abort_callback = [](void * data) -> bool {
        const auto & cancellation = *static_cast<const cancel_state *>(data);
        return cancellation.callback(cancellation.user) != 0;
    };
    parameters.abort_callback_user_data = &state;
    return whisper_full(context, parameters, mono, count);
}

AK_EXPORT const char * AK_CALL ak_whisper_language(whisper_context * context) {
    const int language = whisper_full_lang_id(context);
    return language >= 0 ? whisper_lang_str(language) : nullptr;
}

AK_EXPORT int AK_CALL ak_whisper_segment_count(whisper_context * context) {
    return whisper_full_n_segments(context);
}

AK_EXPORT const char * AK_CALL ak_whisper_segment(whisper_context * context, int index,
    std::int64_t * start, std::int64_t * end) {
    *start = whisper_full_get_segment_t0(context, index);
    *end = whisper_full_get_segment_t1(context, index);
    return whisper_full_get_segment_text(context, index);
}
