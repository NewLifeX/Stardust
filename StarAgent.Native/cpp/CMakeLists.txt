cmake_minimum_required(VERSION 3.20)

project(StarAgentNative LANGUAGES CXX VERSION 1.0.2026.1017)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

option(STARAGENT_WITH_WS "Build WebSocket notify support (IXWebSocket)" ON)

string(TIMESTAMP STARAGENT_BUILD_TIME "%Y-%m-%d %H:%M:%S" UTC)

if(MSVC)
  add_compile_definitions(_CRT_SECURE_NO_WARNINGS NOMINMAX WIN32_LEAN_AND_MEAN)
  add_compile_options(/utf-8 /W4 /permissive- /Zc:__cplusplus /EHsc)
endif()

find_package(Threads REQUIRED)

# Enable HTTPS/WSS automatically when OpenSSL is available. Users can still
# force the setting with -DSTARAGENT_WITH_TLS=ON/OFF.
find_package(OpenSSL QUIET)
if(NOT DEFINED STARAGENT_WITH_TLS)
  if(OpenSSL_FOUND)
    set(STARAGENT_WITH_TLS_DEFAULT ON)
  else()
    set(STARAGENT_WITH_TLS_DEFAULT OFF)
  endif()
else()
  set(STARAGENT_WITH_TLS_DEFAULT ${STARAGENT_WITH_TLS})
endif()
option(STARAGENT_WITH_TLS "Enable HTTPS/WSS through OpenSSL" ${STARAGENT_WITH_TLS_DEFAULT})
if(STARAGENT_WITH_TLS AND NOT OpenSSL_FOUND)
  find_package(OpenSSL REQUIRED)
endif()
if(NOT STARAGENT_WITH_TLS)
  message(WARNING "OpenSSL not enabled: this build supports http/ws only. Production deployments must use HTTPS/WSS; install OpenSSL and rebuild with -DSTARAGENT_WITH_TLS=ON.")
endif()

if(STARAGENT_WITH_WS)
  include(FetchContent)
  set(USE_TLS ${STARAGENT_WITH_TLS} CACHE BOOL "" FORCE)
  set(USE_OPEN_SSL ${STARAGENT_WITH_TLS} CACHE BOOL "" FORCE)
  set(USE_ZLIB OFF CACHE BOOL "" FORCE)
  set(IXWEBSOCKET_INSTALL OFF CACHE BOOL "" FORCE)
  set(BUILD_SHARED_LIBS OFF CACHE BOOL "" FORCE)
  set(BUILD_DEMO OFF CACHE BOOL "" FORCE)
  FetchContent_Declare(
    ixwebsocket
    GIT_REPOSITORY https://github.com/machinezone/IXWebSocket.git
    GIT_TAG v12.1.0
    GIT_SHALLOW TRUE
  )
  FetchContent_MakeAvailable(ixwebsocket)
endif()

set(STARAGENT_SOURCES
  src/agent.cpp
  src/config.cpp
  src/events.cpp
  src/executor.cpp
  src/http_client.cpp
  src/logging.cpp
  src/metrics.cpp
  src/protocol.cpp
  src/util.cpp
  src/ws_client.cpp
)

if(WIN32)
  list(APPEND STARAGENT_SOURCES src/platform_windows.cpp src/service_windows.cpp)
elseif(APPLE)
  list(APPEND STARAGENT_SOURCES src/platform_macos.cpp src/service_posix.cpp)
elseif(CMAKE_SYSTEM_NAME STREQUAL "Linux")
  list(APPEND STARAGENT_SOURCES src/platform_linux.cpp src/service_posix.cpp)
else()
  message(FATAL_ERROR "Unsupported platform ${CMAKE_SYSTEM_NAME}: Windows, Linux (including OpenWrt) and macOS are supported.")
endif()

add_library(staragent_core STATIC ${STARAGENT_SOURCES})
target_include_directories(staragent_core PUBLIC src)
target_include_directories(staragent_core SYSTEM PUBLIC third_party)
target_compile_definitions(staragent_core PUBLIC
  STARAGENT_VERSION="${PROJECT_VERSION}"
  STARAGENT_BUILD_TIME="${STARAGENT_BUILD_TIME}"
)
target_link_libraries(staragent_core PUBLIC Threads::Threads)

if(WIN32)
  target_link_libraries(staragent_core PUBLIC ws2_32 iphlpapi pdh advapi32)
elseif(APPLE)
  target_link_libraries(staragent_core PUBLIC "-framework CoreFoundation")
endif()

if(STARAGENT_WITH_TLS)
  target_compile_definitions(staragent_core PUBLIC CPPHTTPLIB_OPENSSL_SUPPORT)
  target_link_libraries(staragent_core PUBLIC OpenSSL::SSL OpenSSL::Crypto)
endif()

if(STARAGENT_WITH_WS)
  target_compile_definitions(staragent_core PUBLIC STARAGENT_WITH_WS)
  target_link_libraries(staragent_core PUBLIC ixwebsocket::ixwebsocket)
endif()

add_executable(staragent src/main.cpp)
target_link_libraries(staragent PRIVATE staragent_core)

enable_testing()

add_executable(staragent_tests tests/tests.cpp)
target_link_libraries(staragent_tests PRIVATE staragent_core)

add_test(NAME staragent_unit COMMAND staragent_tests)
add_test(NAME staragent_version COMMAND staragent --version)

if(MSVC)
  set_property(TARGET staragent_tests PROPERTY VS_DEBUGGER_WORKING_DIRECTORY "${CMAKE_BINARY_DIR}")
endif()

install(TARGETS staragent RUNTIME DESTINATION bin)
