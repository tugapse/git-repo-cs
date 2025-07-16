@echo off
REM This script builds the .NET application for Windows and Linux,
REM and generates platform-specific run scripts (run.cmd and run.sh).

REM --- Define Colors for Output ---
REM Using PowerShell for colored output in CMD
set "GREEN=powershell -NoProfile -Command "Write-Host -ForegroundColor Green" "
set "YELLOW=powershell -NoProfile -Command "Write-Host -ForegroundColor Yellow" "
set "RED=powershell -NoProfile -Command "Write-Host -ForegroundColor Red" "
set "BLUE=powershell -NoProfile -Command "Write-Host -ForegroundColor Blue" "
set "NC=powershell -NoProfile -Command "Write-Host -NoNewline" "

REM --- Configuration ---
REM IMPORTANT: Set this to the actual name of your .NET project's executable (e.g., 'git-repo' if your .csproj is 'git-repo.csproj')
set "PROJECT_EXECUTABLE_NAME=git-repo"
REM IMPORTANT: Set this to the base name you want for your zip file artifacts (e.g., 'git-repo-cs')
set "RELEASE_ARTIFACT_NAME=git-repo-cs"

set "RELEASE_DIR=.\publish\releases" REM Define the release output directory

REM --- Check for dotnet CLI ---
%YELLOW% --- Checking for dotnet CLI ---
where.exe dotnet >nul 2>&1
if %errorlevel% neq 0 (
    %RED% Error: 'dotnet' command not found. Please install the .NET SDK. Exiting.
    exit /b 1
) else (
    %GREEN% dotnet CLI found.
)

REM --- Create Release Directory (This block will be skipped as ZIP_AVAILABLE is not set to true) ---
REM In the original bash snippet, ZIP_AVAILABLE was not explicitly set to 'true',
REM causing this block to be skipped. Replicating that behavior here.
if "%ZIP_AVAILABLE%"=="true" (
    %YELLOW% --- Ensuring release directory exists: %RELEASE_DIR% ---
    if not exist "%RELEASE_DIR%" (
        mkdir "%RELEASE_DIR%"
        if not exist "%RELEASE_DIR%" (
            %RED% Error: Failed to create directory %RELEASE_DIR%. Check permissions or disk space.
            REM set "ZIP_AVAILABLE=false" (Not needed as this block is already skipped)
        )
    )
)

REM Get the absolute path for the release directory (This block will also be skipped)
if "%ZIP_AVAILABLE%"=="true" (
    for /f "tokens=*" %%i in ('powershell -NoProfile -Command "Resolve-Path '%RELEASE_DIR%'"') do (
        set "ABS_RELEASE_DIR=%%i"
    )
    if not defined ABS_RELEASE_DIR (
        %RED% Error: Failed to resolve absolute path for %RELEASE_DIR%. Exiting.
        exit /b 1
    )
)

REM --- Build and Publish for Windows ---
%GREEN% --- Publishing for Windows (win-x64) ---
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishDir=.\publish\win-x64
if %errorlevel% neq 0 (
    %RED% Error: dotnet publish for Windows failed. Exiting.
    exit /b 1
)

REM --- Generate run.cmd for Windows ---
%YELLOW% --- Generating run.cmd for Windows ---
(
echo @echo off
echo REM This script runs the Windows x64 version of the application.
echo REM Navigate to the published application directory
echo cd .\publish\win-x64 ^|^| (echo Error: Windows publish directory not found. ^& exit /b 1)
echo REM Execute the application
echo .\!PROJECT_EXECUTABLE_NAME!.exe
) > run.cmd
%GREEN% Created: run.cmd

REM --- Build and Publish for Linux ---
%GREEN% --- Publishing for Linux (linux-x64) ---
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishDir=.\publish\linux-x64
if %errorlevel% neq 0 (
    %RED% Error: dotnet publish for Linux failed. Exiting.
    exit /b 1
)

REM --- Generate run.sh for Linux ---
%YELLOW% --- Generating run.sh for Linux ---
(
echo #!/bin/bash
echo # This script runs the Linux x64 version of the application.
echo # Navigate to the published application directory
echo cd ./publish/linux-x64 ^|^| { echo "Error: Linux publish directory not found."; exit 1; }
echo # Execute the application
echo ./%PROJECT_EXECUTABLE_NAME%
) > run.sh
REM chmod +x run.sh is not applicable in a .cmd script, but the content of run.sh is correct.
%GREEN% Created: run.sh

%GREEN% --- All operations complete! ---