<#
.SYNOPSIS
    Builds a .NET application for Windows and Linux, and generates platform-specific run scripts.

.DESCRIPTION
    This script automates the process of:
    1.  Checking for the 'dotnet' CLI and attempting to install it if missing via dotnet-install.ps1.
    2.  Defining configuration variables for application name and release directory.
    3.  Publishing the .NET application for Windows (win-x64) as a self-contained executable.
    4.  Generating a 'run.cmd' script for Windows.
    5.  Publishing the .NET application for Linux (linux-x64) as a self-contained executable.
    6.  Generating a 'run.sh' script for Linux.
    It provides color-coded output for better readability and error handling.

.NOTES
    - Requires PowerShell 5.0 or later for 'Compress-Archive' (though zipping is not in this version's scope).
    - Ensure 'dotnet' CLI is installed or 'dotnet-install.ps1' is available in the same directory.
    - Update $PROJECT_EXECUTABLE_NAME and $RELEASE_ARTIFACT_NAME to match your project's details.
#>

# --- Define Colors for Output ---
# Using Write-Host -ForegroundColor for native PowerShell coloring
# You can also use ANSI escape sequences if your terminal supports them:
# $Green = "`e[0;32m"
# $Yellow = "`e[0;33m"
# $Red = "`e[0;31m"
# $Blue = "`e[0;34m"
# $NoColor = "`e[0m"

# --- Configuration ---
# IMPORTANT: Set this to the actual name of your .NET project's executable (e.g., 'git-repo' if your .csproj is 'git-repo.csproj')
$PROJECT_EXECUTABLE_NAME = "git-repo"
# IMPORTANT: Set this to the base name you want for your zip file artifacts (e.g., 'git-repo-cs')
$RELEASE_ARTIFACT_NAME = "git-repo-cs"

$RELEASE_DIR = ".\publish\releases" # Define the release output directory

# Get the directory where the current script is located
$SCRIPT_DIR = $PSScriptRoot

# Path to the dotnet-install PowerShell script
$DOTNET_INSTALL_SCRIPT = Join-Path $SCRIPT_DIR "dotnet-install.ps1"

# ZIP_AVAILABLE is not explicitly set to true in the original .cmd,
# so we'll default it to false to match its behavior for the relevant blocks.
$ZipAvailable = $false

# --- Function to check and install dotnet CLI ---
function CheckAndInstallDotNet {
    Write-Host "--- Checking for dotnet CLI ---" -ForegroundColor Yellow
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Write-Host "Error: 'dotnet' command not found." -ForegroundColor Red
        if (Test-Path -Path $DOTNET_INSTALL_SCRIPT -PathType Leaf) {
            Write-Host "Attempting to run dotnet-install.ps1..." -ForegroundColor Yellow
            try {
                # Execute the install script
                & $DOTNET_INSTALL_SCRIPT
                if ($LASTEXITCODE -ne 0) {
                    Write-Host "Error: dotnet-install.ps1 failed (Exit Code: $LASTEXITCODE). Please install .NET SDK manually. Exiting." -ForegroundColor Red
                    exit 1
                }
                # Re-check dotnet after install attempt
                if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
                    Write-Host "Error: 'dotnet' command still not found after attempting install. Please install it manually. Exiting." -ForegroundColor Red
                    exit 1
                } else {
                    Write-Host "dotnet CLI found after install attempt." -ForegroundColor Green
                }
            } catch {
                Write-Host "Error executing dotnet-install.ps1: $($_.Exception.Message). Exiting." -ForegroundColor Red
                exit 1
            }
        } else {
            Write-Host "Error: dotnet-install.ps1 not found at '$DOTNET_INSTALL_SCRIPT'. Please install .NET SDK manually. Exiting." -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "dotnet CLI found." -ForegroundColor Green
    }
}

# Call the function to check/install dotnet
CheckAndInstallDotNet

# --- Create Release Directory (This block will be skipped as $ZipAvailable is false) ---
if ($ZipAvailable) {
    Write-Host "--- Ensuring release directory exists: $RELEASE_DIR ---" -ForegroundColor Yellow
    try {
        New-Item -ItemType Directory -Path $RELEASE_DIR -Force | Out-Null
        if (-not (Test-Path -Path $RELEASE_DIR -PathType Container)) {
            Write-Host "Error: Failed to create directory $RELEASE_DIR. Check permissions or disk space." -ForegroundColor Red
            # $ZipAvailable = $false (Not needed as this block is already skipped)
        }
    } catch {
        Write-Host "Error creating directory '$RELEASE_DIR': $($_.Exception.Message)." -ForegroundColor Red
    }
}

# Get the absolute path for the release directory (This block will also be skipped)
if ($ZipAvailable) {
    try {
        $ABS_RELEASE_DIR = (Resolve-Path $RELEASE_DIR).Path
    } catch {
        Write-Host "Error: Failed to resolve absolute path for '$RELEASE_DIR'. Exiting." -ForegroundColor Red
        exit 1
    }
}

# --- Build and Publish for Windows ---
Write-Host "--- Publishing for Windows (win-x64) ---" -ForegroundColor Green
try {
    dotnet publish -c Release -r win-x64 --self-contained true -p:PublishDir=".\publish\win-x64"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Error: dotnet publish for Windows failed (Exit Code: $LASTEXITCODE). Exiting." -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "Error during Windows publish: $($_.Exception.Message). Exiting." -ForegroundColor Red
    exit 1
}

# --- Generate run.cmd for Windows ---
Write-Host "--- Generating run.cmd for Windows ---" -ForegroundColor Yellow
$runCmdContent = @"
@echo off
REM This script runs the Windows x64 version of the application.
REM Navigate to the published application directory
cd .\publish\win-x64 || (echo Error: Windows publish directory not found. & exit /b 1)
REM Execute the application
.\${PROJECT_EXECUTABLE_NAME}.exe "%*"
"@
$runCmdContent | Set-Content -Path "run.cmd" -Force
Write-Host "Created: run.cmd" -ForegroundColor Green

# --- Build and Publish for Linux ---
Write-Host "--- Publishing for Linux (linux-x64) ---" -ForegroundColor Green
try {
    dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishDir=".\publish\linux-x64"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Error: dotnet publish for Linux failed (Exit Code: $LASTEXITCODE). Exiting." -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "Error during Linux publish: $($_.Exception.Message). Exiting." -ForegroundColor Red
    exit 1
}

# --- Generate run.sh for Linux ---
Write-Host "--- Generating run.sh for Linux ---" -ForegroundColor Yellow
$runShContent = @"
#!/bin/bash
# This script runs the Linux x64 version of the application.
# Navigate to the published application directory
cd ./publish/linux-x64 || { echo "Error: Linux publish directory not found."; exit 1; }
# Execute the application
./${PROJECT_EXECUTABLE_NAME} "$@"
"@
$runShContent | Set-Content -Path "run.sh" -Force
# On Windows, we can't directly chmod. The user would need to do this on Linux.
# Write-Host "chmod +x run.sh is not applicable in a .ps1 script, but the content of run.sh is correct." -ForegroundColor Yellow
Write-Host "Created: run.sh" -ForegroundColor Green

Write-Host "--- All operations complete! ---" -ForegroundColor Green