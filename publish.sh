#!/bin/bash

# Define ASCII colors for output
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# --- Configuration ---
# IMPORTANT: Replace 'YourAppName' with the actual name you want for your zip files
APP_NAME="git-repo-cs"
VERSION_FILE="./GitRepoPy/GlobalConfig.cs" # Adjust this path to your file containing SCRIPT_VERSION
RELEASE_DIR="./publish/releases" # New: Define the release output directory

# --- Check for 'zip' command ---
if ! command -v zip &> /dev/null; then
    echo -e "${RED}Warning: 'zip' command not found. Zipping will be skipped.${NC}"
    ZIP_AVAILABLE=false
else
    ZIP_AVAILABLE=true
fi

# --- Extract Version ---
echo -e "${YELLOW}--- Extracting version from ${VERSION_FILE} ---${NC}"
# Use grep to find the version string
SCRIPT_VERSION=$(grep -oP 'public const string SCRIPT_VERSION = "\K[^"]+' "$VERSION_FILE")

if [ -z "$SCRIPT_VERSION" ]; then
    echo -e "${RED}Error: SCRIPT_VERSION not found in ${VERSION_FILE}. Exiting.${NC}"
    exit 1
fi

echo -e "${BLUE}===================================${NC}"
echo -e "${BLUE}  Application Version: ${SCRIPT_VERSION}  ${NC}"
echo -e "${BLUE}===================================${NC}"

# --- Create Release Directory (if zipping is enabled) ---
# Create the directory once, right after version detection, before any builds/zips
if [ "$ZIP_AVAILABLE" = true ]; then
    echo -e "${YELLOW}--- Ensuring release directory exists: ${RELEASE_DIR} ---${NC}"
    mkdir -p "$RELEASE_DIR"
    # Verify directory creation
    if [ ! -d "$RELEASE_DIR" ]; then
        echo -e "${RED}Error: Failed to create directory ${RELEASE_DIR}. Check permissions for $(pwd) or disk space.${NC}"
        ZIP_AVAILABLE=false # Disable zipping if directory creation fails
    fi
fi

# Get the absolute path for the release directory for robust zipping
# This ensures zip always has a clear path regardless of current directory
if [ "$ZIP_AVAILABLE" = true ]; then
    ABS_RELEASE_DIR=$(realpath "$RELEASE_DIR")
    if [ $? -ne 0 ]; then
        echo -e "${RED}Error: Failed to resolve absolute path for ${RELEASE_DIR}. Exiting.${NC}"
        exit 1
    fi
fi

# --- Build and Publish for Windows ---
echo -e "${GREEN}--- Publishing for Windows (win-x64) ---${NC}"
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishDir=./publish/win-x64

# --- Conditionally Compress Windows Output ---
if [ "$ZIP_AVAILABLE" = true ]; then
    echo -e "${YELLOW}--- Zipping Windows output ---${NC}"
    ZIP_NAME="${APP_NAME}_win-x64_v${SCRIPT_VERSION}.zip"
    # Navigate to publish directory, zip contents to ABS_RELEASE_DIR, then navigate back
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

# --- Conditionally Compress Linux Output ---
if [ "$ZIP_AVAILABLE" = true ]; then
    echo -e "${YELLOW}--- Zipping Linux output ---${NC}"
    ZIP_NAME="${APP_NAME}_linux-x64_v${SCRIPT_VERSION}.zip"
    # Navigate to publish directory, zip contents to ABS_RELEASE_DIR, then navigate back
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