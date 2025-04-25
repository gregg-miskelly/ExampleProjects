#!/bin/sh

export DOTNET_DbgMiniDumpType=4
export DOTNET_DbgMiniDumpName=/tmp/%e-%p.dmp
export DOTNET_DbgEnableMiniDump=1
#ulimit -c unlimited

out/Linux/bin/x64.Debug/CsExe