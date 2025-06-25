using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using GitRepoPy; // For other helper classes

namespace GitRepoPy
{
    /// <summary>
    /// Contains the core business logic for managing Python projects: setup, update, and removal.
    /// </summary>
    public static class ProjectService
    {
        /// <summary>Checks for the presence of required system dependencies (git, python3).</summary>
        public static async Task CheckSystemDependencies()
        {
            Logger.LogInfo("Checking for required system tools...");
            var missingTools = new List<string>();

            // Check for Git
            var (gitExitCode, _, _) = await CommandExecutor.RunCommandAsync("git", "--version", logOutput: false);
            if (gitExitCode != 0) missingTools.Add("git");

            // Check for Python 3
            var (pythonExitCode, _, _) = await CommandExecutor.RunCommandAsync("python3", "--version", logOutput: false);
            if (pythonExitCode != 0) missingTools.Add("python3");

            if (missingTools.Any())
            {
                Logger.LogError($"The following required system tools are not installed or not in PATH: {string.Join(", ", missingTools)}. Please install them and try again. For 'python3 -m venv' functionality, ensure your Python 3 installation includes the 'venv' module (e.g., on Debian/Ubuntu: 'sudo apt install python3-venv').");
            }
            Logger.LogInfo("All required system tools found.");
        }

        /// <summary>Removes a Python project, its symlink, and its directory after user confirmation.</summary>
        /// <param name="repoName">The name of the repository/project to remove.</param>
        public static void RemoveProject(string repoName)
        {
            var projectDir = Path.Combine(GlobalConfig.TOOLS_BASE_DIR, repoName);
            var symlinkDest = Path.Combine(GlobalConfig.TOOLS_BIN_DIR, repoName);

            Logger.LogInfo($"Initiating removal process for repository '{repoName}'...");

            Console.WriteLine($"{GlobalConfig.WARN_COLOR}WARNING: This will permanently delete the symbolic link at '{symlinkDest}' and the entire directory '{projectDir}'.{GlobalConfig.RESET_COLOR}");
            Console.Write("Are you absolutely sure you want to proceed? (type 'yes' to confirm): ");
            var confirmation = Console.ReadLine();

            if (!"yes".Equals(confirmation?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogInfo("Removal cancelled by user.");
                Environment.Exit(0);
            }

            // Remove symlink or file in TOOLS_BIN_DIR.
            // Bash `rm -f` handles files and file symlinks.
            if (File.Exists(symlinkDest) || File.Exists(symlinkDest + ".exe") || File.Exists(symlinkDest + ".cmd")) // Consider Windows executable suffixes
            {
                try
                {
                    Logger.LogInfo($"Removing executable/symbolic link: '{symlinkDest}'...");
                    File.Delete(symlinkDest);
                    // Also attempt to delete common Windows executable suffixes for the symlink name
                    File.Delete(symlinkDest + ".exe");
                    File.Delete(symlinkDest + ".cmd");
                    Logger.LogInfo($"Executable/symbolic link '{symlinkDest}' removed.");
                }
                catch (Exception ex)
                {
                    Logger.LogWarn($"Failed to remove executable/symbolic link '{symlinkDest}': {ex.Message}. Manual removal may be required. Please check permissions.");
                }
            }
            else
            {
                Logger.LogWarn($"Executable/symbolic link '{symlinkDest}' does not exist or is not a symlink/file. Skipping removal.");
            }

            // Remove project directory using the robust deletion method
            if (Directory.Exists(projectDir))
            {
                Logger.LogInfo($"Removing project directory: '{projectDir}'...");
                // Safety check for project_dir against TOOLS_BASE_DIR, similar to bash script
                // Ensure we are deleting something under TOOLS_BASE_DIR and not the base itself or root.
                if (projectDir.StartsWith(GlobalConfig.TOOLS_BASE_DIR + Path.DirectorySeparatorChar.ToString()) || projectDir.Equals(GlobalConfig.TOOLS_BASE_DIR, StringComparison.OrdinalIgnoreCase))
                {
                    FileSystemHelper.DeleteDirectoryRobustly(projectDir, true); // true for recursive
                }
                else
                {
                    Logger.LogError($"Attempted to remove an invalid or potentially dangerous path: '{projectDir}'. Refusing to proceed.", 1);
                }
            }
            else
            {
                Logger.LogWarn($"Project directory '{projectDir}' does not exist. Skipping directory removal.");
            }

            Logger.LogInfo($"Removal process for '{repoName}' completed.");
            Environment.Exit(0);
        }

        /// <summary>Updates a Git repository: cleans pycache, stashes changes, pulls, pops stash.</summary>
        /// <param name="repoName">The name of the repository/project to update.</param>
        public static async Task UpdateProject(string repoName)
        {
            var projectDir = Path.Combine(GlobalConfig.TOOLS_BASE_DIR, repoName);
            bool stashPushed = false;

            Logger.LogInfo($"Initiating update process for repository '{repoName}'...");

            if (!Directory.Exists(projectDir))
            {
                Logger.LogError($"Project directory '{projectDir}' does not exist. Cannot update.");
            }

            if (!Directory.Exists(Path.Combine(projectDir, ".git")))
            {
                Logger.LogError($"Project directory '{projectDir}' is not a Git repository. Cannot update.");
            }

            // Clean __pycache__ directories
            Logger.LogInfo($"Cleaning up '__pycache__' directories in '{projectDir}'...");
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(projectDir, "__pycache__", SearchOption.AllDirectories))
                {
                    Logger.LogInfo($"Deleting: {dir}");
                    FileSystemHelper.DeleteDirectoryRobustly(dir, true); // Use robust deletion for pycache too
                }
                Logger.LogInfo("'__pycache__' directories cleaned.");
            }
            catch (Exception ex)
            {
                Logger.LogWarn($"Failed to remove some '__pycache__' directories: {ex.Message}. This might indicate permission issues or active processes.");
            }

            Logger.LogInfo($"Checking for local changes in '{projectDir}'...");
            // Check for modified (staged/unstaged) or untracked files
            var (diffQuietExitCode, _, _) = await CommandExecutor.RunCommandAsync("git", "diff --quiet --exit-code", projectDir, logOutput: false);
            var (diffCachedQuietExitCode, _, _) = await CommandExecutor.RunCommandAsync("git", "diff --cached --quiet --exit-code", projectDir, logOutput: false);
            var (lsFilesOthersExitCode, lsFilesOthersOut, _) = await CommandExecutor.RunCommandAsync("git", "ls-files --others --exclude-standard", projectDir, logOutput: false);

            bool hasLocalChanges = diffQuietExitCode != 0 || diffCachedQuietExitCode != 0 || !string.IsNullOrWhiteSpace(lsFilesOthersOut);

            if (hasLocalChanges)
            {
                Logger.LogInfo("Local changes detected. Stashing them before pull.");
                var (stashPushExitCode, _, stashPushErr) = await CommandExecutor.RunCommandAsync("git", $"stash push -u -m \"git-repo-py update: temporary stash for {repoName}\"", projectDir);
                if (stashPushExitCode != 0)
                {
                    Logger.LogWarn($"Failed to stash local changes: {stashPushErr}. Attempting pull without stashing, but this may lead to merge issues if there are conflicts. Please check manually.");
                }
                else
                {
                    stashPushed = true;
                    Logger.LogInfo("Local changes stashed.");
                }
            }
            else
            {
                Logger.LogInfo("No local changes detected. Skipping stash.");
            }

            Logger.LogInfo($"Pulling latest changes for '{repoName}'...");
            var (pullExitCode, _, pullErr) = await CommandExecutor.RunCommandAsync("git", "pull", projectDir);
            if (pullExitCode != 0)
            {
                Logger.LogWarn($"Git pull failed for '{repoName}': {pullErr}. You may have merge conflicts or other issues that require manual resolution. (Exit status: {pullExitCode})");
                // Do not exit here, attempt to pop stash if it was pushed
            }
            else
            {
                Logger.LogInfo("Git pull completed successfully.");
            }

            if (stashPushed)
            {
                Logger.LogInfo("Attempting to pop stashed changes...");
                var (stashPopExitCode, _, stashPopErr) = await CommandExecutor.RunCommandAsync("git", "stash pop", projectDir);
                if (stashPopExitCode != 0)
                {
                    Logger.LogWarn($"Failed to pop stashed changes for '{repoName}': {stashPopErr}. This might be due to merge conflicts. You may need to resolve conflicts and run 'git stash pop' manually.");
                }
                else
                {
                Logger.LogInfo("Stashed changes popped successfully.");
                }
            }

            Logger.LogInfo($"Update process for '{repoName}' completed.");
            Environment.Exit(0);
        }

        /// <summary>
        /// Handles the main logic for setting up a Python project: cloning, venv, dependencies, and run script.
        /// </summary>
        /// <param name="repoName">The name of the repository/project.</param>
        /// <param name="githubUrl">The GitHub URL of the repository.</param>
        /// <param name="forceCreateRunMode">If true, always creates a default Python wrapper script,
        ///                                   even if a 'run.sh' exists in the project.</param>
        public static async Task SetupProject(string repoName, string githubUrl, bool forceCreateRunMode)
        {
            Logger.LogInfo($"Starting project setup script (Version: {GlobalConfig.SCRIPT_VERSION})...");

            await CheckSystemDependencies();

            var projectDir = Path.Combine(GlobalConfig.TOOLS_BASE_DIR, repoName);
            var venvDir = Path.Combine(projectDir, ".venv");
            var requirementsFile = Path.Combine(projectDir, "requirements.txt");
            var buildScript = Path.Combine(projectDir, "build.sh");

            Logger.LogInfo($"Ensuring base directory '{GlobalConfig.TOOLS_BASE_DIR}' exists...");
            try
            {
                Directory.CreateDirectory(GlobalConfig.TOOLS_BASE_DIR);
                Logger.LogInfo($"Base directory '{GlobalConfig.TOOLS_BASE_DIR}' created or already exists.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to create base directory '{GlobalConfig.TOOLS_BASE_DIR}': {ex.Message}. Please check permissions.", 1);
            }

            Logger.LogInfo($"Checking repository '{repoName}' at '{projectDir}'...");
            if (Directory.Exists(projectDir))
            {
                Logger.LogWarn($"Repository '{repoName}' already exists at '{projectDir}'. Skipping cloning.");
            }
            else
            {
                Logger.LogInfo($"Cloning '{githubUrl}' to '{projectDir}'...");

                // Mimic bash script's `env -i ... git clone` for cleaner environment
                var gitCloneEnv = new Dictionary<string, string?> // Changed to string? to allow null values
                {
                    { "HOME", Path.GetTempPath() }, // Use system temp path
                    { "GIT_ASKPASS", "" },
                    { "GIT_TERMINAL_PROMPT", "0" },
                    { "GITHUB_TOKEN", null },        // Unset sensitive token
                    { "GIT_SSH_COMMAND", null }      // Unset custom SSH command
                    // PATH is automatically inherited and not explicitly added here, relying on default behavior of ProcessStartInfo
                };

                var (gitCloneExitCode, gitCloneOut, gitCloneErr) = await CommandExecutor.RunCommandAsync("git", $"clone \"{githubUrl}\" \"{projectDir}\"", Environment.CurrentDirectory, environmentVariables: gitCloneEnv);
                if (gitCloneExitCode != 0)
                {
                    Logger.LogError($"Failed to clone repository '{githubUrl}': {gitCloneErr}. Check network access and URL.", 1);
                }
                Logger.LogInfo($"Repository '{repoName}' cloned successfully.");
            }

            Logger.LogInfo($"Setting up virtual environment for '{repoName}' at '{venvDir}'...");
            if (Directory.Exists(venvDir))
            {
                Logger.LogWarn($"Virtual environment for '{repoName}' already exists at '{venvDir}'. Skipping creation.");
            }
            else
            {
                var (venvExitCode, venvOut, venvErr) = await CommandExecutor.RunCommandAsync("python3", $"-m venv \"{venvDir}\"", projectDir);
                if (venvExitCode != 0)
                {
                    Logger.LogError($"Failed to create virtual environment for '{repoName}': {venvErr}. Ensure 'python3-venv' or similar package is installed.", 1);
                }
                Logger.LogInfo($"Virtual environment for '{repoName}' created.");
            }

            // Determine correct venv bin directory (Scripts on Windows, bin on Unix-like)
            var venvBinDir = Path.Combine(venvDir, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Scripts" : "bin");
            if (Directory.Exists(venvBinDir))
            {
                Logger.LogInfo($"Making virtual environment executables in '{venvBinDir}' group-executable (if applicable)...");
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) // Only apply chmod on Unix-like systems
                {
                    try
                    {
                        foreach (var file in Directory.EnumerateFiles(venvBinDir))
                        {
                            FileSystemHelper.SetExecutablePermissions(file);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarn($"Error setting permissions in '{venvBinDir}': {ex.Message}");
                    }
                }
            }

            if (File.Exists(buildScript))
            {
                Logger.LogInfo($"Found '{buildScript}' for '{repoName}'. Running build script instead of installing from requirements.txt...");
                FileSystemHelper.SetExecutablePermissions(buildScript); // Make build.sh executable

                var (buildExitCode, buildOut, buildErr) = await CommandExecutor.RunCommandAsync(buildScript, "", projectDir);
                if (buildExitCode != 0)
                {
                    Logger.LogError($"Build script '{buildScript}' failed for '{repoName}': {buildErr}. Check its output for details.", 1);
                }
                Logger.LogInfo($"Build script '{buildScript}' executed successfully.");
            }
            else if (File.Exists(requirementsFile))
            {
                Logger.LogInfo($"No 'build.sh' found. Installing dependencies for '{repoName}' from '{requirementsFile}'...");
                // Direct path to pip within the virtual environment
                var pipExecutable = Path.Combine(venvBinDir, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "pip.exe" : "pip");

                if (!File.Exists(pipExecutable))
                {
                    Logger.LogError($"pip executable not found at '{pipExecutable}'. Cannot install dependencies.", 1);
                }

                // In C#, we don't "source" the activate script. We just call pip directly using its full path.
                var (pipExitCode, pipOut, pipErr) = await CommandExecutor.RunCommandAsync(pipExecutable, $"install -r \"{requirementsFile}\" --no-input --disable-pip-version-check", projectDir);
                if (pipExitCode != 0)
                {
                    Logger.LogError($"Failed to install dependencies for '{repoName}': {pipErr}. Check '{requirementsFile}' and log for detailed pip errors.", 1);
                }
                Logger.LogInfo($"Dependencies for '{repoName}' installed.");
            }
            else
            {
                Logger.LogWarn($"Neither 'build.sh' nor 'requirements.txt' found for '{repoName}'. Skipping dependency/build step.");
            }

            // --- Start of run.sh handling logic ---
            var targetBinExecutable = Path.Combine(GlobalConfig.TOOLS_BIN_DIR, repoName);
            var projectRunScriptPath = Path.Combine(projectDir, "run.sh");
            var projectMainPyPath = Path.Combine(projectDir, "main.py"); // Path to main.py within the cloned project

            Logger.LogInfo($"Ensuring target bin directory '{GlobalConfig.TOOLS_BIN_DIR}' exists...");
            try
            {
                Directory.CreateDirectory(GlobalConfig.TOOLS_BIN_DIR);
                Logger.LogInfo($"Target bin directory '{GlobalConfig.TOOLS_BIN_DIR}' created or already exists.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to create target bin directory '{GlobalConfig.TOOLS_BIN_DIR}': {ex.Message}. Please check permissions.", 1);
            }

            // Remove any existing executable/symlink at the target bin path to prevent conflicts.
            if (File.Exists(targetBinExecutable) || File.Exists(targetBinExecutable + ".exe") || File.Exists(targetBinExecutable + ".cmd")) // Account for Windows extensions
            {
                Logger.LogWarn($"Existing file or symlink found at '{targetBinExecutable}'. Removing it before creating a new one.");
                try
                {
                    File.Delete(targetBinExecutable);
                    File.Delete(targetBinExecutable + ".exe");
                    File.Delete(targetBinExecutable + ".cmd");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to remove existing file/symlink at '{targetBinExecutable}': {ex.Message}. Please check permissions.", 1);
                }
            }

            if (File.Exists(projectRunScriptPath) && !forceCreateRunMode)
            {
                // Scenario 1: project_dir/run.sh found AND --force-create-run is NOT active
                Logger.LogInfo($"'run.sh' found in project directory ('{projectRunScriptPath}'). Making it executable and creating a symbolic link.");
                FileSystemHelper.SetExecutablePermissions(projectRunScriptPath);

                FileSystemHelper.CreateSymlink(projectRunScriptPath, targetBinExecutable);
            }
            else
            {
                // Scenario 2: project_dir/run.sh NOT found OR --force-create-run IS active
                if (forceCreateRunMode)
                {
                    Logger.LogInfo($"Force-creating default Python wrapper script at '{targetBinExecutable}' (ignoring existing project 'run.sh' if any).");
                }
                else
                {
                    Logger.LogWarn($"No 'run.sh' found in project directory ('{projectRunScriptPath}'). Generating default Python wrapper script directly at '{targetBinExecutable}'.");
                }

                // Generate the wrapper script content. This will be a *Bash* script, written by the C# app.
                var wrapperScriptContent = new StringBuilder();
                wrapperScriptContent.AppendLine("#!/bin/bash");
                wrapperScriptContent.AppendLine($"# This script was automatically generated by the project setup script (Version: {GlobalConfig.SCRIPT_VERSION}).");
                wrapperScriptContent.AppendLine("# It acts as a wrapper to run the main Python application within its virtual environment.");
                wrapperScriptContent.AppendLine("");
                wrapperScriptContent.AppendLine($"PROJECT_ROOT=\"{projectDir}\"");
                wrapperScriptContent.AppendLine($"VENV_DIR=\"${{PROJECT_ROOT}}/.venv\"");
                wrapperScriptContent.AppendLine($"MAIN_PYTHON_SCRIPT=\"${{PROJECT_ROOT}}/main.py\"");
                wrapperScriptContent.AppendLine($"ACTIVATE_SCRIPT=\"${{VENV_DIR}}/{(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Scripts" : "bin")}/activate\"");
                wrapperScriptContent.AppendLine("");
                wrapperScriptContent.AppendLine("# Basic checks");
                wrapperScriptContent.AppendLine("if [ ! -d \"$PROJECT_ROOT\" ]; then");
                wrapperScriptContent.AppendLine("    echo \"ERROR: Project directory '$PROJECT_ROOT' not found or accessible.\" >&2");
                wrapperScriptContent.AppendLine("    exit 1");
                wrapperScriptContent.AppendLine("fi");
                wrapperScriptContent.AppendLine("");
                wrapperScriptContent.AppendLine("if [ ! -f \"$MAIN_PYTHON_SCRIPT\" ]; then");
                wrapperScriptContent.AppendLine("    echo \"ERROR: Main Python script '$MAIN_PYTHON_SCRIPT' not found in project directory.\" >&2");
                wrapperScriptContent.AppendLine("    echo \"Please ensure 'main.py' exists in '$PROJECT_ROOT' or adjust the generated script.\" >&2");
                wrapperScriptContent.AppendLine("    exit 1");
                wrapperScriptContent.AppendLine("fi");
                wrapperScriptContent.AppendLine("");
                wrapperScriptContent.AppendLine("if [ ! -f \"$ACTIVATE_SCRIPT\" ]; then");
                wrapperScriptContent.AppendLine("    echo \"ERROR: Virtual environment activation script not found at '$ACTIVATE_SCRIPT'.\" >&2");
                wrapperScriptContent.AppendLine("    echo \"Please ensure the virtual environment is correctly set up in '$VENV_DIR'.\" >&2");
                wrapperScriptContent.AppendLine("    exit 1");
                wrapperScriptContent.AppendLine("fi");
                wrapperScriptContent.AppendLine("");
                wrapperScriptContent.AppendLine("# Activate the virtual environment");
                wrapperScriptContent.AppendLine("source \"$ACTIVATE_SCRIPT\"");
                wrapperScriptContent.AppendLine("if [ $? -ne 0 ]; then");
                wrapperScriptContent.AppendLine("    echo \"ERROR: Failed to activate virtual environment at '$ACTIVATE_SCRIPT'.\" >&2");
                wrapperScriptContent.AppendLine("    exit 1");
                wrapperScriptContent.AppendLine("fi");
                wrapperScriptContent.AppendLine("");
                wrapperScriptContent.AppendLine("# Execute the main Python script with all passed arguments");
                wrapperScriptContent.AppendLine("python \"$MAIN_PYTHON_SCRIPT\" \"$@\"");
                wrapperScriptContent.AppendLine("RUN_STATUS=$?");
                wrapperScriptContent.AppendLine("");
                // This logic is always the same for Bash scripts, regardless of OS where C# app runs
                wrapperScriptContent.AppendLine("# Deactivate the virtual environment if the function exists");
                wrapperScriptContent.AppendLine("if declare -f deactivate &>/dev/null; then");
                wrapperScriptContent.AppendLine("    deactivate");
                wrapperScriptContent.AppendLine("fi");
                wrapperScriptContent.AppendLine("");
                wrapperScriptContent.AppendLine("exit $RUN_STATUS");

                try
                {
                    await File.WriteAllTextAsync(targetBinExecutable, wrapperScriptContent.ToString());
                    Logger.LogInfo($"Wrapper script created at '{targetBinExecutable}'.");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to create wrapper script at '{targetBinExecutable}': {ex.Message}. Please check permissions.", 1);
                }

                FileSystemHelper.SetExecutablePermissions(targetBinExecutable);
                Logger.LogInfo($"Wrapper script for '{repoName}' created successfully at '{targetBinExecutable}'.");
            }
            // --- End of run.sh handling logic ---

            // After successful setup and run script creation, add to PATH
            PathEnvironmentManager.AddToolsBinToSystemPath();

            Logger.LogInfo($"Project setup for '{repoName}' completed successfully!");
        }
    }
}
