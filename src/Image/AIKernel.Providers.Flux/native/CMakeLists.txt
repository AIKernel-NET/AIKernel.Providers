cmake_minimum_required(VERSION 3.21)
project(aikernel_flux LANGUAGES C CXX)

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
set(SD_CUDA OFF CACHE BOOL "" FORCE)
set(GGML_NATIVE OFF CACHE BOOL "" FORCE)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)
add_subdirectory("${AIKERNEL_FLUX_ENGINE}" engine)

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
set(generated "${CMAKE_CURRENT_BINARY_DIR}/model_loader_aikernel.cpp")
file(CONFIGURE OUTPUT "${generated}" CONTENT "#include <cstdint>\n#include <cstddef>\nbool ak_flux_read_weight(const char*, std::uint64_t, void*, std::size_t);\n${loader}" @ONLY)
get_target_property(sources stable-diffusion SOURCES)
list(FILTER sources EXCLUDE REGEX "(^|/)model_loader\\.cpp$")
set_property(TARGET stable-diffusion PROPERTY SOURCES "${sources}")
target_sources(stable-diffusion PRIVATE "${generated}")
target_include_directories(stable-diffusion PRIVATE "${AIKERNEL_FLUX_ENGINE}/src")

add_library(aikernel_flux SHARED flux_abi.cpp)
target_compile_features(aikernel_flux PRIVATE cxx_std_17)
target_compile_options(aikernel_flux PRIVATE $<$<CXX_COMPILER_ID:MSVC>:/EHsc>)
target_link_libraries(aikernel_flux PRIVATE stable-diffusion)
set_target_properties(aikernel_flux PROPERTIES CXX_VISIBILITY_PRESET hidden)
install(TARGETS aikernel_flux RUNTIME DESTINATION . LIBRARY DESTINATION .)
