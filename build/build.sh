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

# Get the directory where the current script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" &>/dev/null && pwd)"

# Path to the dotnet-install script
DOTNET_INSTALL_SCRIPT="${SCRIPT_DIR}/dotnet-install.sh"

# --- Function to check and install dotnet ---
check_and_install_dotnet() {
    echo -e "${YELLOW}--- Checking for dotnet CLI ---${NC}"
    if ! command -v dotnet &> /dev/null; then
        echo -e "${RED}Error: 'dotnet' command not found.${NC}"
        if [ -f "${DOTNET_INSTALL_SCRIPT}" ]; then
            echo -e "${YELLOW}Attempting to run dotnet-install.sh...${NC}"
            # Execute the install script
            bash "${DOTNET_INSTALL_SCRIPT}"
            # After trying to install, check again
            if ! command -v dotnet &> /dev/null; then
                echo -e "${RED}Error: 'dotnet' command still not found after attempting install. Please install it manually. Exiting.${NC}"
                exit 1
            else
                echo -e "${GREEN}dotnet CLI found after install attempt.${NC}"
            fi
        else
            echo -e "${RED}Error: dotnet-install.sh not found at ${DOTNET_INSTALL_SCRIPT}. Please install .NET SDK manually. Exiting.${NC}"
            exit 1
        fi
    else
        echo -e "${GREEN}dotnet CLI found.${NC}"
    fi
}

# Call the function at the beginning
check_and_install_dotnet


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
./${PROJECT_EXECUTABLE_NAME} "\$@"
EOF
chmod +x run.sh # Make the script executable
echo -e "${GREEN}Created: run.sh${NC}"

echo -e "${GREEN}--- All operations complete! ---${NC}"