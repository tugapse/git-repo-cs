<#
.SYNOPSIS
    Builds a .NET application for Windows and Linux, extracts the version from a C# file,
    and then zips the published output into a 'releases' subfolder.

.DESCRIPTION
    This script automates the process of:
    1.  Defining configuration variables for application name, version file path, and release directory.
    2.  Checking for the 'Compress-Archive' cmdlet (PowerShell's built-in zip utility).
    3.  Extracting the SCRIPT_VERSION constant from a specified C# file.
    4.  Creating a dedicated 'releases' directory for the final zip files.
    5.  Publishing the .NET application for Windows (win-x64) as a self-contained executable.
    6.  Zipping the Windows published output.
    7.  Publishing the .NET application for Linux (linux-x64) as a self-contained executable.
    8.  Zipping the Linux published output.
    It provides color-coded output for better readability and error handling.

.NOTES
    - Requires PowerShell 5.0 or later for 'Compress-Archive'.
    - Ensure 'dotnet' CLI is installed and accessible in your PATH.
    - Update $APP_NAME and $VERSION_FILE to match your project's details.
    - If 'Compress-Archive' is not available, zipping steps will be skipped with a warning.
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
# IMPORTANT: Replace 'git-repo-cs' with the actual name you want for your zip files
$APP_NAME = "git-repo-cs"
# IMPORTANT: Adjust this path to your file containing public const string SCRIPT_VERSION
$VERSION_FILE = "./GitRepoPy/GlobalConfig.cs"
$RELEASE_DIR = "./publish/releases" # Define the release output directory

# --- Check for 'Compress-Archive' cmdlet ---
$ZipAvailable = $true
if (-not (Get-Command Compress-Archive -ErrorAction SilentlyContinue)) {
    Write-Host "Warning: 'Compress-Archive' cmdlet not found. Zipping will be skipped." -ForegroundColor Red
    $ZipAvailable = $false
}

# --- Extract Version ---
Write-Host "--- Extracting version from $VERSION_FILE ---" -ForegroundColor Yellow

$SCRIPT_VERSION = $null
try {
    # Read the content of the version file
    $versionFileContent = Get-Content $VERSION_FILE -Raw

    # Use Select-String with a regex pattern to find the version
    $match = $versionFileContent | Select-String -Pattern 'public const string SCRIPT_VERSION = "([^"]+)"'

    if ($match) {
        $SCRIPT_VERSION = $match.Groups[1].Value
    } else {
        Write-Host "Error: SCRIPT_VERSION not found in $VERSION_FILE. Exiting." -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "Error reading version file '$VERSION_FILE': $($_.Exception.Message). Exiting." -ForegroundColor Red
    exit 1
}

# Display the detected version with a header
Write-Host "===================================" -ForegroundColor Blue
Write-Host "  Application Version: $SCRIPT_VERSION  " -ForegroundColor Blue
Write-Host "===================================" -ForegroundColor Blue

# --- Create Release Directory (if zipping is enabled) ---
if ($ZipAvailable) {
    Write-Host "--- Ensuring release directory exists: $RELEASE_DIR ---" -ForegroundColor Yellow
    try {
        # Create the directory and its parents if they don't exist
        New-Item -ItemType Directory -Path $RELEASE_DIR -Force | Out-Null
        # Verify directory creation
        if (-not (Test-Path -Path $RELEASE_DIR -PathType Container)) {
            Write-Host "Error: Failed to create directory $RELEASE_DIR. Check permissions or disk space." -ForegroundColor Red
            $ZipAvailable = $false # Disable zipping if directory creation fails
        }
    } catch {
        Write-Host "Error creating directory '$RELEASE_DIR': $($_.Exception.Message). Zipping will be skipped." -ForegroundColor Red
        $ZipAvailable = $false
    }
}

# --- Build and Publish for Windows ---
Write-Host "--- Publishing for Windows (win-x64) ---" -ForegroundColor Green
try {
    # Call dotnet publish. The -p:PublishDir works similarly to bash.
    dotnet publish -c Release -r win-x64 --self-contained true -p:PublishDir=./publish/win-x64
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Error: dotnet publish for Windows failed (Exit Code: $LASTEXITCODE). Exiting." -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "Error during Windows publish: $($_.Exception.Message). Exiting." -ForegroundColor Red
    exit 1
}

# --- Conditionally Compress Windows Output ---
if ($ZipAvailable) {
    Write-Host "--- Zipping Windows output ---" -ForegroundColor Yellow
    $zipFileName = "${APP_NAME}_win-x64_v${SCRIPT_VERSION}.zip"
    $sourcePath = Join-Path (Get-Location) "publish\win-x64"
    $destinationPath = Join-Path (Get-Location) $RELEASE_DIR $zipFileName

    try {
        # Compress-Archive requires the source path to be a directory, not a file pattern
        # So we zip the contents of the directory
        Compress-Archive -Path "$sourcePath\*" -DestinationPath $destinationPath -Force
        Write-Host "Created: $destinationPath" -ForegroundColor Green
    } catch {
        Write-Host "Error: Failed to create Windows zip file. $($_.Exception.Message). Check permissions for $RELEASE_DIR or disk space." -ForegroundColor Red
    }
} else {
    Write-Host "Skipping Windows output zipping (Compress-Archive not available or directory creation failed)." -ForegroundColor Yellow
}

# --- Build and Publish for Linux ---
Write-Host "--- Publishing for Linux (linux-x64) ---" -ForegroundColor Green
try {
    dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishDir=./publish/linux-x64
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Error: dotnet publish for Linux failed (Exit Code: $LASTEXITCODE). Exiting." -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "Error during Linux publish: $($_.Exception.Message). Exiting." -ForegroundColor Red
    exit 1
}

# --- Conditionally Compress Linux Output ---
if ($ZipAvailable) {
    Write-Host "--- Zipping Linux output ---" -ForegroundColor Yellow
    $zipFileName = "${APP_NAME}_linux-x64_v${SCRIPT_VERSION}.zip"
    $sourcePath = Join-Path (Get-Location) "publish\linux-x64"
    $destinationPath = Join-Path (Get-Location) $RELEASE_DIR $zipFileName

    try {
        Compress-Archive -Path "$sourcePath\*" -DestinationPath $destinationPath -Force
        Write-Host "Created: $destinationPath" -ForegroundColor Green
    } catch {
        Write-Host "Error: Failed to create Linux zip file. $($_.Exception.Message). Check permissions for $RELEASE_DIR or disk space." -ForegroundColor Red
    }
} else {
    Write-Host "Skipping Linux output zipping (Compress-Archive not available or directory creation failed)." -ForegroundColor Yellow
}

Write-Host "--- All operations complete! ---" -ForegroundColor Green