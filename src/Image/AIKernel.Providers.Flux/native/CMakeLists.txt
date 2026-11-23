cmake_minimum_required(VERSION 3.21)
project(aikernel_flux LANGUAGES C CXX)

option(AIKERNEL_FLUX_CUDA "Build the separate CUDA image editing provider" OFF)

set(AIKERNEL_FLUX_ENGINE "" CACHE PATH "Checkout of pinned stable-diffusion.cpp, with ggml initialized")
find_package(Git REQUIRED)
execute_process(COMMAND "${GIT_EXECUTABLE}" -C "${AIKERNEL_FLUX_ENGINE}" rev-parse HEAD
    OUTPUT_VARIABLE engine_revision OUTPUT_STRIP_TRAILING_WHITESPACE COMMAND_ERROR_IS_FATAL ANY)
execute_process(COMMAND "${GIT_EXECUTABLE}" -C "${AIKERNEL_FLUX_ENGINE}/ggml" rev-parse HEAD
    OUTPUT_VARIABLE ggml_revision OUTPUT_STRIP_TRAILING_WHITESPACE COMMAND_ERROR_IS_FATAL ANY)
if(NOT engine_revision STREQUAL "0d71f963af9c94941f4a67e8ea145935651dd756" OR
   NOT ggml_revision STREQUAL "eeda9ec48fc84a3df0bb9dcc19ae9053075a2539")
    message(FATAL_ERROR "Use the stable-diffusion.cpp and ggml revisions named in the FLUX provider guide")
endif()

set(SD_BUILD_EXAMPLES OFF CACHE BOOL "" FORCE)
set(SD_BUILD_SHARED_LIBS OFF CACHE BOOL "" FORCE)
set(SD_BUILD_SHARED_GGML_LIB OFF CACHE BOOL "" FORCE)
set(SD_WEBP OFF CACHE BOOL "" FORCE)
set(SD_WEBM OFF CACHE BOOL "" FORCE)
set(SD_CUDA ${AIKERNEL_FLUX_CUDA} CACHE BOOL "" FORCE)
set(GGML_NATIVE OFF CACHE BOOL "" FORCE)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)
add_subdirectory("${AIKERNEL_FLUX_ENGINE}" engine)

if(AIKERNEL_FLUX_CUDA)
    find_package(CUDAToolkit REQUIRED)
    set(bridge aikernel_flux_cuda)
else()
    set(bridge aikernel_flux)
endif()

# Replace only the actual tensor payload read. Model headers stay with the
# upstream parser. The managed callback supplies all GGUF and safetensors data.
set(loader_path "${AIKERNEL_FLUX_ENGINE}/src/model_loader.cpp")
set_property(DIRECTORY APPEND PROPERTY CMAKE_CONFIGURE_DEPENDS "${loader_path}")
file(READ "${loader_path}" loader)
string(REPLACE "\r\n" "\n" loader "${loader}")
set(original [=[                            file.seekg(tensor_storage.offset);
                            file.read(buf, n);
                            if (!file) {]=])
set(replacement [=[                            if (!ak_flux_read_weight(file_path.c_str(), tensor_storage.offset, buf, n)) {]=])
string(FIND "${loader}" "${original}" read_location)
if(read_location LESS 0)
    message(FATAL_ERROR "The upstream tensor read site changed")
endif()
string(REPLACE "${original}" "${replacement}" loader "${loader}")
set(loader_declarations "#include <cstdint>\n#include <cstddef>\nbool ak_flux_read_weight(const char*, std::uint64_t, void*, std::size_t);\n")
if(AIKERNEL_FLUX_CUDA)
    # Write ordinary contiguous CUDA tensors straight from a borrowed device page.
    # Keep upstream conversion and host tensor handling for other representations.
    set(original [=[                    auto read_data = [&](char* buf, size_t n) -> bool {]=])
    set(replacement [=[                    if (!zip && !mmapped && dst_tensor->buffer != nullptr &&
                        tensor_storage.type == dst_tensor->type && !tensor_storage.is_f64 && !tensor_storage.is_i64 &&
#ifdef SD_USE_UPSTREAM_GGML
                        !tensor_storage.is_f8_e4m3 && !tensor_storage.is_f8_e5m2 &&
#endif
                        ggml_is_contiguous(dst_tensor) && nbytes_to_read == ggml_nbytes(dst_tensor)) {
                        int direct = ak_flux_read_cuda_weight(
                            ggml_backend_buft_name(ggml_backend_buffer_get_type(dst_tensor->buffer)),
                            file_path.c_str(), tensor_storage.offset, dst_tensor->data, nbytes_to_read);
                        if (direct < 0) {
                            failed = true;
                            break;
                        }
                        if (direct > 0) {
                            read_time_ms.fetch_add(ggml_time_ms() - t0);
                            bytes_processed.fetch_add(static_cast<uint64_t>(nbytes_to_read));
                            continue;
                        }
                    }

                    auto read_data = [&](char* buf, size_t n) -> bool {]=])
    string(FIND "${loader}" "${original}" read_location)
    if(read_location LESS 0)
        message(FATAL_ERROR "The upstream CUDA tensor loading site changed")
    endif()
    string(REPLACE "${original}" "${replacement}" loader "${loader}")
    string(APPEND loader_declarations "int ak_flux_read_cuda_weight(const char*, const char*, std::uint64_t, void*, std::size_t);\n")
endif()
set(generated "${CMAKE_CURRENT_BINARY_DIR}/model_loader_aikernel.cpp")
file(CONFIGURE OUTPUT "${generated}" CONTENT "${loader_declarations}${loader}" @ONLY)
get_target_property(sources stable-diffusion SOURCES)
list(FILTER sources EXCLUDE REGEX "(^|/)model_loader\\.cpp$")
set_property(TARGET stable-diffusion PROPERTY SOURCES "${sources}")
target_sources(stable-diffusion PRIVATE "${generated}")
target_include_directories(stable-diffusion PRIVATE "${AIKERNEL_FLUX_ENGINE}/src")

add_library(${bridge} SHARED flux_abi.cpp)
target_compile_features(${bridge} PRIVATE cxx_std_17)
target_compile_options(${bridge} PRIVATE $<$<CXX_COMPILER_ID:MSVC>:/EHsc>)
target_link_libraries(${bridge} PRIVATE stable-diffusion)
if(AIKERNEL_FLUX_CUDA)
    target_compile_definitions(${bridge} PRIVATE AK_FLUX_CUDA)
    target_sources(${bridge} PRIVATE flux_cuda_pages.cpp)
    target_link_libraries(${bridge} PRIVATE CUDA::cuda_driver)
endif()
set_target_properties(${bridge} PROPERTIES CXX_VISIBILITY_PRESET hidden)
install(TARGETS ${bridge} RUNTIME DESTINATION . LIBRARY DESTINATION .)
