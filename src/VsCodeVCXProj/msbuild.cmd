@echo off

REM
REM This is a helper script for invoking msbuild. All the arguments are set along to msbuild.
REM 

set MSBuildArgs=%*
if "%~1"=="/?" set MSBuildArgs=-?

set MSBuildPath=
call :SetFromPathEnv msbuild.exe
if NOT "%MSBuildPath%"=="" goto MSBuildFound

set x86ProgramFiles=%ProgramFiles(x86)%
if "%x86ProgramFiles%"=="" set x86ProgramFiles=%ProgramFiles%
set VSWherePath=%x86ProgramFiles%\Microsoft Visual Studio\Installer\vswhere.exe

if NOT exist "%VSWherePath%" echo ERROR: Could not find vswhere.exe (%VSWherePath%) & exit /b -1 

for /f "usebackq tokens=1 delims=" %%a in (`"%VSWherePath%" -prerelease -property installationPath`) do call :ProcessIDE "%%a"
if NOT "%MSBuildPath%"=="" goto MSBuildFound

echo ERROR: Unable to find a Visual Studio install.
exit /b -1

:MSBuildFound
echo "%MSBuildPath%" %MSBuildArgs%
call "%MSBuildPath%" %MSBuildArgs%
exit /b %ERRORLEVEL%

:ProcessIDE
if NOT "%MSBuildPath%"=="" goto :EOF
if exist "%~1\MSBuild\15.0\Bin\MSBuild.exe"    set MSBuildPath=%~1\MSBuild\15.0\Bin\MSBuild.exe& goto :EOF
if exist "%~1\MSBuild\Current\Bin\MSBuild.exe" set MSBuildPath=%~1\MSBuild\Current\Bin\MSBuild.exe& goto :EOF
goto :EOF

:SetFromPathEnv
set MSBUildPath=%~$PATH:1
goto :EOF
