cmake_minimum_required(VERSION 2.8.12)

project(hello_world)

if (APPLE)
    set(CMAKE_INSTALL_RPATH "@executable_path")
else(UNIX)
    set(CMAKE_INSTALL_RPATH "\$ORIGIN")
endif()

add_compile_options(-std=c++17)
add_library(MyNativeLib SHARED Lib.cpp)
set_property(TARGET MyNativeLib APPEND_STRING PROPERTY COMPILE_FLAGS "-fvisibility=hidden")

target_link_libraries(MyNativeLib stdc++)
# add_install(TARGETS MyNativeLib RUNTIME)
install(TARGETS MyNativeLib
    DESTINATION .)