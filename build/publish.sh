#!/bin/bash

# Define ASCII colors for output
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
RED='\033[0;31m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# --- Configuration ---
# IMPORTANT: Set this to the actual name of your .NET project's executable (e.g., 'git-repo' if your .csproj is 'git-repo.csproj')
PROJECT_EXECUTABLE_NAME="git-repo"
# IMPORTANT: Set this to the base name you want for your zip file artifacts (e.g., 'git-repo-cs')
RELEASE_ARTIFACT_NAME="git-repo-cs"
# IMPORTANT: Adjust this path to your file containing public const string SCRIPT_VERSION
VERSION_FILE="./GitRepoPy/GlobalConfig.cs"
RELEASE_DIR="./publish/releases" # Define the release output directory

# --- Check for dotnet CLI ---
echo -e "${YELLOW}--- Checking for dotnet CLI ---${NC}"
if ! command -v dotnet &> /dev/null
then
    echo -e "${RED}Error: 'dotnet' command not found. Please install the .NET SDK. Exiting.${NC}"
    exit 1
else
    echo -e "${GREEN}dotnet CLI found.${NC}"
fi

# --- Check for 'zip' command ---
echo -e "${YELLOW}--- Checking for 'zip' utility ---${NC}"
if ! command -v zip &> /dev/null
then
    echo -e "${RED}Warning: 'zip' command not found. Zipping will be skipped for both platforms.${NC}"
    ZIP_AVAILABLE=false
else
    echo -e "${GREEN}zip utility found.${NC}"
    ZIP_AVAILABLE=true
fi

# --- Extract Version ---
echo -e "${YELLOW}--- Extracting version from ${VERSION_FILE} ---${NC}"
SCRIPT_VERSION=$(grep -oP 'public const string SCRIPT_VERSION = "\K[^"]+' "$VERSION_FILE")

if [ -z "$SCRIPT_VERSION" ]; then
    echo -e "${RED}Error: SCRIPT_VERSION not found in ${VERSION_FILE}. Exiting.${NC}"
    exit 1
fi

echo -e "${BLUE}===================================${NC}"
echo -e "${BLUE}  Application Version: ${SCRIPT_VERSION}  ${NC}"
echo -e "${BLUE}===================================${NC}"

# --- Create Release Directory (if zipping is enabled) ---
if [ "$ZIP_AVAILABLE" = true ]; then
    echo -e "${YELLOW}--- Ensuring release directory exists: ${RELEASE_DIR} ---${NC}"
    mkdir -p "$RELEASE_DIR"
    if [ ! -d "$RELEASE_DIR" ]; then
        echo -e "${RED}Error: Failed to create directory ${RELEASE_DIR}. Check permissions for $(pwd) or disk space. Zipping disabled.${NC}"
        ZIP_AVAILABLE=false
    fi
fi

# Get the absolute path for the release directory for robust zipping
if [ "$ZIP_AVAILABLE" = true ]; then
    ABS_RELEASE_DIR=$(realpath "$RELEASE_DIR")
    if [ $? -ne 0 ]; then
        echo -e "${RED}Error: Failed to resolve absolute path for ${RELEASE_DIR}. Zipping disabled. Exiting.${NC}"
        ZIP_AVAILABLE=false # Disable zipping if realpath fails
    fi
fi

# --- Build and Publish for Windows ---
echo -e "${GREEN}--- Publishing for Windows (win-x64) ---${NC}"
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishDir=./publish/win-x64
if [ $? -ne 0 ]; then
    echo -e "${RED}Error: dotnet publish for Windows failed. Exiting.${NC}"
    exit 1
fi

# --- Generate run.cmd for Windows ---
echo -e "${YELLOW}--- Generating run.cmd for Windows ---${NC}"
cat <<EOF > run.cmd
@echo off
REM This script runs the Windows x64 version of the application.
REM Navigate to the published application directory
cd .\publish\win-x64 || (echo Error: Windows publish directory not found. & exit /b 1)
REM Execute the application
.\%PROJECT_EXECUTABLE_NAME%.exe
EOF
echo -e "${GREEN}Created: run.cmd${NC}"

# --- Conditionally Compress Windows Output ---
if [ "$ZIP_AVAILABLE" = true ]; then
    echo -e "${YELLOW}--- Zipping Windows output ---${NC}"
    ZIP_NAME="${RELEASE_ARTIFACT_NAME}_win-x64_v${SCRIPT_VERSION}.zip"
    (cd ./publish/win-x64 && zip -r "${ABS_RELEASE_DIR}/${ZIP_NAME}" ./*)
    if [ $? -eq 0 ]; then
        echo -e "${GREEN}Created: ${ABS_RELEASE_DIR}/${ZIP_NAME}${NC}"
    else
        echo -e "${RED}Error: Failed to create Windows zip file. Check permissions for ${ABS_RELEASE_DIR} or disk space.${NC}"
    fi
else
    echo -e "${YELLOW}Skipping Windows output zipping (zip command not available or directory creation failed).${NC}"
fi

# --- Build and Publish for Linux ---
echo -e "${GREEN}--- Publishing for Linux (linux-x64) ---${NC}"
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishDir=./publish/linux-x64
if [ $? -ne 0 ]; then
    echo -e "${RED}Error: dotnet publish for Linux failed. Exiting.${NC}"
    exit 1
fi

# --- Generate run.sh for Linux ---
echo -e "${YELLOW}--- Generating run.sh for Linux ---${NC}"
cat <<EOF > run.sh
#!/bin/bash
# This script runs the Linux x64 version of the application.
# Navigate to the published application directory
cd ./publish/linux-x64 || { echo "Error: Linux publish directory not found."; exit 1; }
# Execute the application
./${PROJECT_EXECUTABLE_NAME}
EOF
chmod +x run.sh # Make the script executable
echo -e "${GREEN}Created: run.sh${NC}"

# --- Conditionally Compress Linux Output ---
if [ "$ZIP_AVAILABLE" = true ]; then
    echo -e "${YELLOW}--- Zipping Linux output ---${NC}"
    ZIP_NAME="${RELEASE_ARTIFACT_NAME}_linux-x64_v${SCRIPT_VERSION}.zip"
    (cd ./publish/linux-x64 && zip -r "${ABS_RELEASE_DIR}/${ZIP_NAME}" ./*)
    if [ $? -eq 0 ]; then
        echo -e "${GREEN}Created: ${ABS_RELEASE_DIR}/${ZIP_NAME}${NC}"
    else
        echo -e "${RED}Error: Failed to create Linux zip file. Check permissions for ${ABS_RELEASE_DIR} or disk space.${NC}"
    fi
else
    echo -e "${YELLOW}Skipping Linux output zipping (zip command not available or directory creation failed).${NC}"
fi

echo -e "${GREEN}--- All operations complete! ---${NC}"