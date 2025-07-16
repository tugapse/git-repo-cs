@echo off
REM This script runs the Windows x64 version of the application.
REM Navigate to the published application directory
cd .\publish\win-x64 || (echo Error: Windows publish directory not found. & exit /b 1)
REM Execute the application
.\%PROJECT_EXECUTABLE_NAME%.exe
