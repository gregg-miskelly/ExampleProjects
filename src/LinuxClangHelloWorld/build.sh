#!/bin/bash

script_dir=`dirname $0`

print_install_instructions()
{
    echo "Install clang and cmake to build"    
}

usage()
{
    echo "Script for building this project"
    echo ""
    echo 'Usage: $0 [BuildArch] [BuildType] [clean] [verbose] [CMakeTrace] [clangx.y] [cross] [musl]'
    echo "BuildArch can be: x64, arm, arm64"
    echo "BuildType can be: Debug, Release"
    echo "clean - optional argument to force a clean build."
    echo "verbose - optional argument to enable verbose build output."
    echo "CMakeTrace - generate a CMake trace, but do not actually build."
    echo "clangx.y - optional argument to build using clang version x.y."
    echo "cross - optional argument to signify cross-compilation."
    echo "musl - optional argument to signify compiling with musl."
    echo "      - requires thats ROOTFS_DIR environment variable is set."
    exit 1
}

setup_dirs()
{
    echo Setting up directories for build

    mkdir -p "$__BinDir"
    mkdir -p "$__LogsDir"
    mkdir -p "$__IntermediatesDir"
}

# Performs "clean build" type actions (deleting and remaking directories)

clean()
{
    echo Cleaning previous output for the selected configuration
    rm -rf "$__BinDir"

    if [ $? -ne 0 ]
    then
        echo "ERROR: unable to remove directory '$__BinDir'."
        exit 1
    fi

    rm -rf "$__IntermediatesDir"
    if [ $? -ne 0 ]
    then
        echo "ERROR: unable to remove directory '$__IntermediatesDir'."
        exit 1
    fi

    rm -rf "$__LogsDir/*${__BuildOS}__${__BuildArch}__${__BuildType}.*"
}

# Check the system to ensure the right pre-reqs are in place

check_prereqs()
{
    echo "Checking pre-requisites..."

    # Check presence of CMake on the path
    hash cmake 2>/dev/null || { echo >&2 "Please install cmake before running this script"; print_install_instructions; exit 1; }
}

locate_llvm_exec() 
{
    if which "$llvm_prefix$1$desired_llvm_version" > /dev/null 2>&1
    then
    echo "$(which $llvm_prefix$1$desired_llvm_version)"
    elif which "$llvm_prefix$1" > /dev/null 2>&1
    then
    echo "$(which $llvm_prefix$1)"
    else
    exit 1
    fi
}

locate_build_tools()
{
    desired_llvm_major_version=$__ClangMajorVersion
    desired_llvm_minor_version=$__ClangMinorVersion
    if [ $OSName == "FreeBSD" ]; then
        desired_llvm_version="$desired_llvm_major_version$desired_llvm_minor_version"
    elif [ $OSName == "OpenBSD" ]; then
        desired_llvm_version=""
    elif [ $OSName == "NetBSD" ]; then
        desired_llvm_version=""
    else
      desired_llvm_version="-$desired_llvm_major_version.$desired_llvm_minor_version"
    fi

    llvm_ar="$(locate_llvm_exec ar)"
    [[ $? -eq 0 ]] || { echo "Unable to locate llvm-ar"; exit 1; }
    llvm_link="$(locate_llvm_exec link)"
    [[ $? -eq 0 ]] || { echo "Unable to locate llvm-link"; exit 1; }
    llvm_nm="$(locate_llvm_exec nm)"
    [[ $? -eq 0 ]] || { echo "Unable to locate llvm-nm"; exit 1; }
    if [ $OSName = "Linux" -o $OSName = "FreeBSD" -o $OSName = "OpenBSD" -o $OSName = "NetBSD" ]; then
      llvm_objdump="$(locate_llvm_exec objdump)"
      [[ $? -eq 0 ]] || { echo "Unable to locate llvm-objdump"; exit 1; }
    fi

    cmake_extra_defines=
    if [[ -n "$LLDB_LIB_DIR" ]]; then
        cmake_extra_defines="$cmake_extra_defines -DWITH_LLDB_LIBS=$LLDB_LIB_DIR"
    fi
    if [[ -n "$LLDB_INCLUDE_DIR" ]]; then
        cmake_extra_defines="$cmake_extra_defines -DWITH_LLDB_INCLUDES=$LLDB_INCLUDE_DIR"
    fi

    # Only pass ROOTFS_DIR if cross is specified and the current platform is not OSX that doesn't use rootfs
    if [[ $__CrossBuild == 1 && "${OSName}" != "Darwin"  ]]; then
        if ! [[ -n "$ROOTFS_DIR" ]]; then
            echo "ROOTFS_DIR not set for cross compile"
            exit 1
        fi
        if [[ $__UseMusl == 1 ]]; then
            cmake_extra_defines="$cmake_extra_defines -DALPINE_CROSS_BUILD=1"
        fi

        if [[ -z "$CONFIG_DIR" ]]; then
            CONFIG_DIR="$EnlistmentRoot/cross/$__BuildArch"
        fi
        cmake_extra_defines="$cmake_extra_defines -DCMAKE_TOOLCHAIN_FILE=$CONFIG_DIR/toolchain.cmake"
        cmake_extra_defines="$cmake_extra_defines -DCLR_UNIX_CROSS_BUILD=1"
    fi

    if [[ $OSName == "Darwin" ]]; then
        # Need to set SDKRoot for newer versions of XCode removed '/usr/includes'.
        # See https://stackoverflow.com/questions/51761599/cannot-find-stdio-h
        export SDKROOT=$(xcrun --sdk macosx --show-sdk-path)
        if [[ "$__BuildArch" == x64 ]]; then
            if [[ "$__HostArch" == "arm64" ]]; then
                __CrossBuild="1"
            fi
            cmake_extra_defines="-DCMAKE_OSX_ARCHITECTURES=x86_64 $cmake_extra_defines"
        elif [[ "$__BuildArch" == arm64 ]]; then
            cmake_extra_defines="-DCMAKE_OSX_ARCHITECTURES=arm64 $cmake_extra_defines"
        else
            echo "Error: Unknown OSX architecture $platformArch."
            exit 1
        fi

        if [[ $__CrossBuild == 1 ]]; then
            cmake_extra_defines="$cmake_extra_defines -DCLR_UNIX_CROSS_BUILD=1"
        fi
    fi
}

invoke_build()
{
    # All set to commence the build

    echo "Commencing build for $__BuildOS.$__BuildArch.$__BuildType using clang $__ClangMajorVersion.$__ClangMinorVersion"
    cd "$__IntermediatesDir"

    locate_build_tools

    cmake_args="-DCMAKE_AR=$llvm_ar"
    cmake_args+=" -DCMAKE_LINKER=$llvm_link"
    cmake_args+=" -DCMAKE_NM=$llvm_nm"
    cmake_args+=" -DCMAKE_OBJDUMP=$llvm_objdump"
    cmake_args+=" -DCMAKE_BUILD_TYPE=$__BuildType"
    cmake_args+=" -DCMAKE_INSTALL_PREFIX=$__BinDir"
    # cmake_args+=" -DREPOSITORY_ROOT=$EnlistmentRoot"
    # cmake_args+=" -DINTERMEDIATES_DIR=$__IntermediatesDir"
    cmake_args+=" $cmake_extra_defines"
    if [[ "$__CMakeTrace" == "1" ]]; then
        cmake_args+=" --trace-expand"
    fi
    cmake_args+=" $EnlistmentRoot"

    # Regenerate the CMake solution
    echo "cmake $cmake_args"
    if [[ "$__CMakeTrace" -ne "1" ]]; then
        if ! cmake $cmake_args; then
            echo "CMake failed!"
            exit 1
        fi
    else
        cmake $cmake_args 2> $__LogsDir/cmake__${__BuildOS}__${__BuildArch}__${__BuildType}.log
        echo "CMake trace written to $__LogsDir/cmake__${__BuildOS}__${__BuildArch}__${__BuildType}.log"
        echo "Aborting further build in CMakeTrace mode"
        exit 1
    fi

    # Check that the makefiles were created.
    if [ ! -f "$__IntermediatesDir/Makefile" ]; then
        echo "Failed to generate native component build project!"
        exit 1
    fi

    # Get the number of processors available to the scheduler
    # Other techniques such as `nproc` only get the number of
    # processors available to a single process.
    if [ `uname` = "FreeBSD" ]; then
        NumProc=`sysctl hw.ncpu | awk '{ print $2+1 }'`
    else
        NumProc=$(($(getconf _NPROCESSORS_ONLN)+1))
    fi

    echo "Executing make install -j $NumProc $__UnprocessedBuildArgs"

    make install -j $NumProc $__UnprocessedBuildArgs > >(tee $__LogsDir/make__${__BuildOS}__${__BuildArch}__${__BuildType}.log) 2> >(tee $__LogsDir/make__${__BuildOS}__${__BuildArch}__${__BuildType}.err >&2)
    if [ $? != 0 ]; then
        # If stdout (handle 1) is a terminal, change the output color to red
        [ -t 1 ] && echo -e "\033[0;31m"
        # Dump the errors
        cat $__LogsDir/make__${__BuildOS}__${__BuildArch}__${__BuildType}.err
        # Turn the output color back to normal
        [ -t 1 ] && echo -e "\033[0;0m"
        echo "Make failed. Standard output can be found at --"
        echo "   cat $__LogsDir/make__${__BuildOS}__${__BuildArch}__${__BuildType}.log"
        echo ""
        exit 1
    fi
}

# Set default clang version
set_clang_path_and_version()
{
    echo "Getting clang version."
    if [[ $__ClangMajorVersion == 0 && $__ClangMinorVersion == 0 ]]; then
        # If the version is not specified, search for one

        # Find the newest version of clang on the PATH
        local ClangVersion=""
        local ClangMaxMajorVersion=0
        local ClangMaxMinorVersion=0
        local oldIFS="$IFS"
        IFS=":"
        for dir in $PATH; do
            for i in "$dir"/clang-*; do
                local clangCommand
                clangCommand="$(basename "$i")"
                local candidateClangVersion="${clangCommand:6}"
                if [[ "$candidateClangVersion" =~ ^[0-9]+$ ]]; then
                    if (( candidateClangVersion >= ClangMaxMajorVersion )); then
                        ClangVersion=$candidateClangVersion
                        ClangMaxMajorVersion=$candidateClangVersion
                        ClangMaxMinorVersion=0
                    fi
                elif [[ "$candidateClangVersion" =~ ^[0-9]+\.[0-9]+$ ]]; then
                    local candidateClangMajorVersion="${candidateClangVersion%.*}"
                    local candidateClangMinorVersion="${candidateClangVersion#*.}"
                    if (( candidateClangMajorVersion > ClangMaxMajorVersion )) || (( candidateClangMajorVersion == ClangMaxMajorVersion && candidateClangMinorVersion > ClangMaxMinorVersion )); then
                        ClangVersion=$candidateClangVersion
                        ClangMaxMajorVersion=$candidateClangMajorVersion
                        ClangMaxMinorVersion=$candidateClangMinorVersion
                    fi
                fi
            done
        done
        IFS="$oldIFS"

        if [ -z "$ClangVersion" ]; then
            # If the 'clang-<ver>' commands weren't installed, scrape version info from the 'clang' command
            hash clang 2>/dev/null
            if [ $? != 0 ]; then
                print_install_instructions
                exit 1
            fi

            ClangVersion=$(clang --version | head -n 1 | grep -o -E "[[:digit:]]+\.[[:digit:]]+\.[[:digit:]]+" | head -n 1)
            
            if [ "${OSName}" == "Darwin" ]; then
                # The Apple version of Clang shows their own version numbers rather than the version numbers of the OSS
                # project. So change ClangVersion to be the version of the OSS software.

                local OSXClangVerArray=(${ClangVersion//./ })
                local OSXClangMajorVer="${OSXClangVerArray[0]}"
                local OSXClangMinorVer="${OSXClangVerArray[1]}"
                local OSXClangPatchVer="${OSXClangVerArray[2]}"

                # Note: data from https://en.wikipedia.org/wiki/Xcode#Toolchain_versions
                if [ "$OSXClangMajorVer" -lt 6 ]; then
                    # 3.6 is the oldest version that we support, so we don't need to handle anything older
                    ClangVersion="3.4"
                elif [ "$OSXClangMajorVer" -eq 6 ]; then
                    if [ "$OSXClangMinorVer" -lt 1 ]; then
                        ClangVersion="3.5"
                    else
                        ClangVersion="3.6"
                    fi
                elif [ "$OSXClangMajorVer" -eq 7 ]; then
                    if [ "$OSXClangMinorVer" -lt 3 ]; then
                        ClangVersion="3.7"
                    else
                        ClangVersion="3.8"
                    fi
                elif [ "$OSXClangMajorVer" -eq 8 ]; then
                    ClangVersion="3.9"
                elif [ "$OSXClangMajorVer" -eq 9 ]; then
                    # Note: Based on the table, the entry should be this.
                    # However with 9.0.0.9000039 it looks like the clang
                    # version is actually 5.
                    # if [ "$OSXClangMinorVer" -lt 1 ]; then
                    #     ClangVersion="4.0"
                    # else
                    #     ClangVersion="5.0"
                    # fi
                    ClangVersion="5.0"
                elif [ "$OSXClangMajorVer" -eq 10 ]; then
                    if [ "$OSXClangPatchVer" -lt 1 ]; then
                        ClangVersion="6.0"
                    else
                        ClangVersion="7.0"
                    fi
                else
                    ClangVersion="8.0"
                fi
            fi
        fi

        local ClangVersionArray=(${ClangVersion//./ })
        __ClangMajorVersion="${ClangVersionArray[0]}"
        __ClangMinorVersion="${ClangVersionArray[1]}"

        if hash "clang-$__ClangMajorVersion.$__ClangMinorVersion" 2>/dev/null
            then
                export CC="$(command -v clang-$__ClangMajorVersion.$__ClangMinorVersion)"
        elif hash "clang-$__ClangMajorVersion" 2>/dev/null
            then
                export CC="$(command -v clang-$__ClangMajorVersion)"
        elif hash clang 2>/dev/null
            then
                export CC="$(command -v clang)"
        else
            echo "Unable to find Clang compiler"
            exit 1
        fi

        if hash "clang++-$__ClangMajorVersion.$__ClangMinorVersion" 2>/dev/null
            then
                export CXX="$(command -v clang++-$__ClangMajorVersion.$__ClangMinorVersion)"
        elif hash "clang++-$__ClangMajorVersion" 2>/dev/null
            then
                export CXX="$(command -v clang++-$__ClangMajorVersion)"
        elif hash clang 2>/dev/null
            then
                export CXX="$(command -v clang++)"
        else
            echo "Unable to find clang++ compiler"
            exit 1
        fi
    else
        # If the version is specified, we require the strongly-versioned executables

        hash "clang-$__ClangMajorVersion.$__ClangMinorVersion" 2>/dev/null
        if [ $? != 0 ]; then
            echo "Specified clang version ($__ClangMajorVersion.$__ClangMinorVersion) was not found"
            exit 1
        fi
        export CC="$(command -v clang-$__ClangMajorVersion.$__ClangMinorVersion)"

        hash "clang++-$__ClangMajorVersion.$__ClangMinorVersion" 2>/dev/null
        if [ $? != 0 ]; then
            echo "Specified clang++ version ($__ClangMajorVersion.$__ClangMinorVersion) was not found"
            exit 1
        fi
        export CXX="$(command -v clang++-$__ClangMajorVersion.$__ClangMinorVersion)"
    fi

    if [[ $__CrossBuild == 1 && ("$__BuildArch" == "arm" || "$__BuildArch" == "armel" || "$__BuildArch" == "arm64") ]]; then
        # Minimum required version of clang is version 3.9 for arm/armel cross build
        if ! [[ "$__ClangMajorVersion" -gt "3" || ( $__ClangMajorVersion == 3 && $__ClangMinorVersion -gt 8 ) ]]; then
            echo "Please install clang 3.9 or latest for arm/armel/arm64 cross build"; exit 1;
        fi
    else
        # 3.6 is required for other scenarios
        if ! [[ "$__ClangMajorVersion" -gt "3" || ( $__ClangMajorVersion == 3 && $__ClangMinorVersion -gt 5 ) ]]; then
            echo "Please install clang 3.6 or latter"; exit 1;
        fi
    fi

    if [ ! -f "$CC" ]; then
        echo "clang path $CC does not exist"
        exit 1
    fi

    if [ ! -f "$CXX" ]; then
        echo "clang++ path $CXX does not exist"
        exit 1
    fi
}

# Set the root of the enlisment
pushd $script_dir > /dev/null
EnlistmentRoot=$(pwd)
popd > /dev/null
if [ ! -f "$EnlistmentRoot/build.sh" ]; then
    echo "ERROR: Unexpected directory structure. Couldn't find project root." >&2
    exit 1
fi

# Use uname to determine what the CPU is.
CPUName=$(uname -p)
# Some Linux platforms report unknown for platform, but the arch for machine.
if [ "$CPUName" == "unknown" ]; then
    CPUName=$(uname -m)
fi
# Handle ARM64 macOS
if [ "$CPUName" == "arm" ] && [ "$(uname -m)" == "arm64" ]; then
    CPUName="aarch64"
fi

case $CPUName in
    i686)
        echo "Unsupported CPU $CPUName detected, build might not succeed!"
        __BuildArch=x86
        __HostArch=x86
        ;;

    x86_64)
        __BuildArch=x64
        __HostArch=x64
        ;;

    armv7l)
        echo "Unsupported CPU $CPUName detected, build might not succeed!"
        __BuildArch=arm
        __HostArch=arm
        ;;

    aarch64)
        __BuildArch=arm64
        __HostArch=arm64
        ;;

    *)
        echo "Unknown CPU $CPUName detected, configuring as if for x64"
        __BuildArch=x64
        __HostArch=x64
        ;;
esac

# Use uname to determine what the OS is.
OSName=$(uname -s)
case $OSName in
    Linux)
        __BuildOS=Linux
        ;;

    Darwin)
        __BuildOS=OSX
        ;;

    FreeBSD)
        __BuildOS=FreeBSD
        echo "FreeBSD is currently not supported" >&2
        exit 1
        ;;

    OpenBSD)
        __BuildOS=OpenBSD
        echo "OpenBSD is currently not supported" >&2
        exit 1
        ;;

    NetBSD)
        __BuildOS=NetBSD
        echo "NetBSD is currently not supported" >&2
        exit 1
        ;;

    *)
        echo "Unsupported OS $OSName detected" >&2
        exit 1
        ;;
esac
__BuildType=Debug

# Set the various build properties here so that CMake and MSBuild can pick them up
__LogsDir="$EnlistmentRoot/out/Logs"
__UnprocessedBuildArgs=
__MSBCleanBuildArgs=
__CleanBuild=false
__VerboseBuild=false
__CMakeTrace=0
__ClangMajorVersion=0
__ClangMinorVersion=0
__CrossBuild=0
__UseMusl=0

for i in "$@"
do
    if [ "$__stdOutRootPath" == "pending" ]; then
        # Assign to __stdOutRootPath, removing any trailing slashes, and escaping any backslashes
        __stdOutRootPath=`echo $i | sed 's/[\\/]*$//g;s*\\\\*\\\\\\\\*g'`
        if [ -z "$__stdOutRootPath" ]; then
            echo "ERROR: Unexpected empty value for stdOutRootPath"
            exit -1
        fi
        continue
    fi

    lowerI="$(echo $i | awk '{print tolower($0)}')"
    case $lowerI in
    -?|-h|--help)
        usage
        exit 1
        ;;
    x64)
        __BuildArch=x64
        ;;
    arm)
        __BuildArch=arm
        ;;
    arm64)
        __BuildArch=arm64
        ;;
    musl)
        __UseMusl=1
        ;;
    debug)
        __BuildType=Debug
        ;;
    release)
        __BuildType=Release
        ;;
    clean)
        __CleanBuild=1
        ;;
    verbose)
        __VerboseBuild=1
        ;;
    cmaketrace)
        __CMakeTrace=1
        ;;
    clang3.6)
        __ClangMajorVersion=3
        __ClangMinorVersion=6
        ;;
    clang3.7)
        __ClangMajorVersion=3
        __ClangMinorVersion=7
        ;;
    clang3.9)
        __ClangMajorVersion=3
        __ClangMinorVersion=9
        ;;
    build-only|no-clr-restore)
        # These modes are now always on, so ignore them if they are passed, but do nothing.
        ;;
    cross)
        __CrossBuild=1
        ;;
    *)
        __UnprocessedBuildArgs="$__UnprocessedBuildArgs $i"
    esac
done

if [ "$__stdOutRootPath" == "pending" ]; then
    echo "ERROR: expected path following 'stdOutRootPath' argument."
    exit 1
fi

# Set the remaining variables based upon the determined build configuration
OSOutRoot=$EnlistmentRoot/out/$__BuildOS
__BinDir="$OSOutRoot/bin/$__BuildArch.$__BuildType"
__IntermediatesDir="$OSOutRoot/Intermediate/$__BuildArch.$__BuildType"

# Configure environment if we are doing a clean build.
if [ $__CleanBuild == 1 ]; then
    clean
fi

# Configure environment if we are doing a verbose build
if [ $__VerboseBuild == 1 ]; then
    export VERBOSE=1
fi

set_clang_path_and_version

# Make the directories necessary for build if they don't exist

setup_dirs

# Check prereqs.

check_prereqs

invoke_build

# Build complete

echo "Repo successfully built."
echo "Product binaries are available at $__BinDir"
exit 0
