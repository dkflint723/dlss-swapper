@echo off
REM Kept as the local entry point, but the work now lives in sync_manifest.ps1 so that running this
REM by hand and the scheduled Manifest sync workflow do the same thing rather than being two copies
REM that drift. The script also says what changed, which curl overwriting a file silently did not.
REM
REM Any argument is passed straight through: -CheckOnly reports without writing either copy.

where /q pwsh
if %errorlevel% equ 0 (
    pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0sync_manifest.ps1" %*
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0sync_manifest.ps1" %*
)
