cmake_minimum_required(VERSION 2.8.12)

project(hello_world)

if (APPLE)
    set(CMAKE_INSTALL_RPATH "@executable_path")
else(UNIX)
    set(CMAKE_INSTALL_RPATH "\$ORIGIN")
endif()

add_compile_options(-std=c++11)
add_executable(HelloWorld HelloWorld.cpp)
set_property(TARGET HelloWorld APPEND_STRING PROPERTY COMPILE_FLAGS "-fvisibility=hidden")

target_link_libraries(HelloWorld stdc++)
# add_install(TARGETS HelloWorld RUNTIME)
install(TARGETS HelloWorld RUNTIME
    DESTINATION .)