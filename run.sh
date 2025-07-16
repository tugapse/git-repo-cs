#!/bin/bash
# This script runs the Linux x64 version of the application.
# Navigate to the published application directory
cd ./publish/linux-x64 || { echo "Error: Linux publish directory not found."; exit 1; }
# Execute the application
./git-repo
