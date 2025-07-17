<#
.SYNOPSIS
    Builds a .NET application for Windows and Linux, and generates platform-specific run scripts.

.DESCRIPTION
    This script automates the process of:
    1.  Checking for the 'dotnet' CLI and attempting to install it if missing via dotnet-install.ps1.
    2.  Defining configuration variables for application name and release directory.
    3.  Publishing the .NET application for Windows (win-x64) as a self-contained executable.
    4.  Generating a 'run.cmd' script for Windows.
    It provides color-coded output for better readability and error handling.

.NOTES
    - Requires PowerShell 5.0 or later for 'Compress-Archive' (though zipping is not in this version's scope).
    - Ensure 'dotnet' CLI is installed or 'dotnet-install.ps1' is available in the same directory.
    - Update $PROJECT_EXECUTABLE_NAME and $RELEASE_ARTIFACT_NAME to match your project's details.
#>


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

Write-Host "--- All operations complete! ---" -ForegroundColor Green