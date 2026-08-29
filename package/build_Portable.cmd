@echo off

call "%~dp0config.cmd"

REM Delete bin and obj directory
rmdir /s /q ..\src\bin\publish\portable\
rmdir /s /q ..\src\obj\

REM create the output folder if it doesn't already exist.
mkdir Output > NUL 2>&1

echo.
echo ################################
echo Compiling cli
echo ################################
echo.

REM First and into the same folder, for the reason spelled out in
REM build_Installer.cmd. Built Release_Portable like the app beside it, because
REM that configuration is what defines PORTABLE in the app project this
REM references - and that is what decides where the database is looked for. A
REM Release cli in a portable build would read a different library than the app
REM it shipped with.
dotnet publish "%cli_csproj_file%" ^
	--runtime win-x64 ^
    --self-contained ^
    --configuration Release_Portable ^
    -p:PublishDir=..\..\src\bin\publish\portable\ || goto :error

echo.
echo ################################
echo Compiling app
echo ################################
echo.

dotnet publish "%csproj_file%" ^
	--runtime win-x64 ^
    --self-contained ^
    --configuration Release_Portable ^
    -p:PublishDir=bin\publish\portable\ || goto :error

REM Everything is fine, go to the end of the file.
goto :end

REM If there was an error output this error message and navigate back to the initial directory 
:error
echo.
echo.
echo ERROR: Failed with error code %errorlevel%.
cd %initial_directory% > NUL 2>&1
exit /b %errorlevel%

:end
exit /b 0