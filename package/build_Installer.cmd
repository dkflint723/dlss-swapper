@echo off

call "%~dp0config.cmd"

REM Delete bin and obj directory
rmdir /s /q ..\src\bin\publish\installer\
rmdir /s /q ..\src\obj\

REM create the output folder if it doesn't already exist.
mkdir Output > NUL 2>&1

echo.
echo ################################
echo Compiling cli
echo ################################
echo.

REM Published first, and into the same folder, so that the app publish that
REM follows overwrites anything the two both produce. The cli references the app
REM project, so its output is very nearly the app's; publishing it second would
REM mean a file built for the cli - resources.pri most of all - sitting where the
REM app expects its own. What survives from this step is dlss-swapper-cli.exe and
REM its two json files, which is all that is wanted.
REM
REM PublishDir is relative to the project, hence the walk back up to src.
dotnet publish "%cli_csproj_file%" ^
	--runtime win-x64 ^
    --self-contained ^
    --configuration Release ^
    -p:PublishDir=..\..\src\bin\publish\installer\ || goto :error

echo.
echo ################################
echo Compiling app
echo ################################
echo.

dotnet publish "%csproj_file%" ^
	--runtime win-x64 ^
    --self-contained ^
    --configuration Release ^
    -p:PublishDir=bin\publish\installer\ || goto :error

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