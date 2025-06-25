using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading; // For Thread.Sleep

namespace GitRepoPy
{
    /// <summary>
    /// Provides utility methods for common file system operations.
    /// </summary>
    public static class FileSystemHelper
    {
        /// <summary>
        /// Sets executable permissions on a file for Unix-like operating systems.
        /// Does nothing on Windows as executability is determined by file extension/association.
        /// </summary>
        /// <param name="filePath">The path to the file.</param>
        public static void SetExecutablePermissions(string filePath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Logger.LogWarn($"Skipping executable permission set for '{filePath}' on Windows. Executability is determined by file association/extension.");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                try
                {
                    // Set owner, group, and others to have execute permissions
                    var currentMode = File.GetUnixFileMode(filePath);
                    File.SetUnixFileMode(filePath, currentMode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
                    Logger.LogInfo($"Set executable permissions for '{filePath}'.");
                }
                catch (Exception ex)
                {
                    Logger.LogWarn($"Could not set executable permissions for '{filePath}': {ex.Message}");
                }
            }
            else
            {
                Logger.LogWarn($"Unsupported OS for setting executable permissions for '{filePath}'.");
            }
        }

        /// <summary>
        /// Creates a symbolic link.
        /// </summary>
        /// <param name="target">The path to the existing file or directory that the link will point to.</param>
        /// <param name="linkPath">The path where the symbolic link will be created.</param>
        public static void CreateSymlink(string target, string linkPath)
        {
            try
            {
                if (File.Exists(target))
                {
                    File.CreateSymbolicLink(linkPath, target);
                    Logger.LogInfo($"Created symbolic file link from '{target}' to '{linkPath}'.");
                }
                else if (Directory.Exists(target))
                {
                    Directory.CreateSymbolicLink(linkPath, target);
                    Logger.LogInfo($"Created symbolic directory link from '{target}' to '{linkPath}'.");
                }
                else
                {
                    Logger.LogError($"Target '{target}' does not exist for symbolic link creation.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to create symbolic link from '{target}' to '{linkPath}': {ex.Message}. This might require administrator/root privileges.", 1);
            }
        }

        /// <summary>
        /// Attempts to delete a directory robustly, handling read-only attributes and retrying on certain errors.
        /// </summary>
        /// <param name="path">The path to the directory to delete.</param>
        /// <param name="recursive">True to delete subdirectories and files in path; otherwise, false.</param>
        /// <param name="maxRetries">Maximum number of retries for deletion.</param>
        /// <param name="retryDelayMs">Delay in milliseconds between retries.</param>
        public static void DeleteDirectoryRobustly(string path, bool recursive, int maxRetries = 3, int retryDelayMs = 100)
        {
            if (!Directory.Exists(path))
            {
                Logger.LogWarn($"Directory '{path}' does not exist. Skipping robust deletion.");
                return;
            }

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    // First, try to clear read-only attributes recursively on files and directories.
                    // This is crucial for Windows to allow deletion.
                    foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            var attributes = File.GetAttributes(file);
                            if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                            {
                                File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarn($"Could not clear read-only attribute for file '{file}': {ex.Message}");
                        }
                    }
                    foreach (var dir in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            var attributes = File.GetAttributes(dir);
                            if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                            {
                                File.SetAttributes(dir, attributes & ~FileAttributes.ReadOnly);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarn($"Could not clear read-only attribute for directory '{dir}': {ex.Message}");
                        }
                    }

                    // Now attempt the actual deletion
                    Directory.Delete(path, recursive);
                    Logger.LogInfo($"Directory '{path}' deleted successfully on attempt {i + 1}.");
                    return; // Success, exit method
                }
                catch (IOException ex) when (ex.HResult == -2147024864 || ex.HResult == -2147024891) // ERROR_SHARING_VIOLATION or ERROR_ACCESS_DENIED (specific to Windows)
                {
                    Logger.LogWarn($"Failed to delete directory '{path}' (Attempt {i + 1}/{maxRetries}): {ex.Message}. Retrying in {retryDelayMs}ms...");
                    Thread.Sleep(retryDelayMs);
                }
                catch (UnauthorizedAccessException ex)
                {
                    Logger.LogWarn($"Unauthorized access when deleting directory '{path}' (Attempt {i + 1}/{maxRetries}): {ex.Message}. Retrying in {retryDelayMs}ms...");
                    Thread.Sleep(retryDelayMs);
                }
                catch (Exception ex)
                {
                    // For any other unexpected exception, log and rethrow or handle as critical error
                    Logger.LogError($"An unexpected error occurred during robust directory deletion of '{path}': {ex.Message}", 1);
                }
            }

            // If we reach here, all retries failed
            Logger.LogError($"Failed to delete directory '{path}' after {maxRetries} attempts. Manual removal may be required. Please check for processes holding locks on files within this directory or verify permissions.", 1);
        }

        // Removed ConvertWindowsPathToUnixPath as it's no longer needed.
    }
}
