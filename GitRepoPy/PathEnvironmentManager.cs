using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using GitRepoPy; // For Logger and GlobalConfig

namespace GitRepoPy
{
    /// <summary>
    /// Manages modifications to the system's PATH environment variable.
    /// </summary>
    public static class PathEnvironmentManager
    {
        /// <summary>
        /// Adds the TOOLS_BIN_DIR to the system's PATH environment variable if not already present.
        /// Handles OS-specific logic.
        /// </summary>
        public static void AddToolsBinToSystemPath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    // Get the current user PATH variable
                    string currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
                    string[] pathParts = currentPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

                    // Check if TOOLS_BIN_DIR is already in the PATH
                    // Use Path.GetFullPath for canonical comparison, ignoring case on Windows
                    if (!pathParts.Any(p => string.Equals(Path.GetFullPath(p), Path.GetFullPath(GlobalConfig.TOOLS_BIN_DIR), StringComparison.OrdinalIgnoreCase)))
                    {
                        string newPath = currentPath;
                        if (!string.IsNullOrWhiteSpace(newPath))
                        {
                            newPath += Path.PathSeparator;
                        }
                        newPath += GlobalConfig.TOOLS_BIN_DIR;

                        Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.User);
                        Logger.LogInfo($"Added '{GlobalConfig.TOOLS_BIN_DIR}' to the current user's PATH environment variable.");
                        Logger.LogInfo("Please open a NEW terminal window for the PATH changes to take effect.");
                    }
                    else
                    {
                        Logger.LogInfo($"'{GlobalConfig.TOOLS_BIN_DIR}' is already in the current user's PATH environment variable. Skipping addition.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarn($"Failed to add '{GlobalConfig.TOOLS_BIN_DIR}' to PATH on Windows: {ex.Message}. You may need to add it manually.");
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Logger.LogInfo("For Linux/macOS, to make project executables globally available, please add the following line to your shell's profile file (e.g., ~/.bashrc, ~/.zshrc):");
                Console.WriteLine($"  export PATH=\"$PATH:{GlobalConfig.TOOLS_BIN_DIR}\"");
                Logger.LogInfo("After adding, run 'source ~/.bashrc' (or your shell's profile file) or open a new terminal window.");
            }
            else
            {
                Logger.LogWarn($"Unsupported OS for automatic PATH modification. Please add '{GlobalConfig.TOOLS_BIN_DIR}' to your system PATH manually.");
            }
        }
    }
}
