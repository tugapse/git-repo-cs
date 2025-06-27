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
            var symlinkDestUnix = Path.Combine(GlobalConfig.TOOLS_BIN_DIR, repoName); // Original Unix-like symlink name
            var symlinkDestWindowsCmd = Path.Combine(GlobalConfig.TOOLS_BIN_DIR, repoName + ".cmd"); // Windows .cmd wrapper
            var symlinkDestWindowsSh = Path.Combine(GlobalConfig.TOOLS_BIN_DIR, repoName + ".sh"); // Windows .sh script (target of .cmd)

            Logger.LogInfo($"Initiating removal process for repository '{repoName}'...");

            string warningSymlinkPath;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                warningSymlinkPath = $"{symlinkDestWindowsCmd} and {symlinkDestWindowsSh}";
            }
            else
            {
                warningSymlinkPath = symlinkDestUnix;
            }

            Console.WriteLine($"{GlobalConfig.WARN_COLOR}WARNING: This will permanently delete the symbolic link(s) at '{warningSymlinkPath}' and the entire directory '{projectDir}'.{GlobalConfig.RESET_COLOR}");
            Console.Write("Are you absolutely sure you want to proceed? (type 'yes' to confirm): ");
            var confirmation = Console.ReadLine();

            if (!"yes".Equals(confirmation?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogInfo("Removal cancelled by user.");
                Environment.Exit(0);
            }

            // Remove symlink or file(s) in TOOLS_BIN_DIR.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    Logger.LogInfo($"Removing generated script files for '{repoName}' from '{GlobalConfig.TOOLS_BIN_DIR}'...");
                    // Remove the .cmd wrapper
                    if (File.Exists(symlinkDestWindowsCmd))
                    {
                        File.Delete(symlinkDestWindowsCmd);
                        Logger.LogInfo($"Deleted '{symlinkDestWindowsCmd}'.");
                    }

                    // Remove the .sh script (which could be a symlink or direct file)
                    if (File.Exists(symlinkDestWindowsSh))
                    {
                        File.Delete(symlinkDestWindowsSh);
                        Logger.LogInfo($"Deleted '{symlinkDestWindowsSh}'.");
                    }

                    // Clean up old non-extension executable/symlink if it existed from previous versions
                    var oldNoExtensionLink = Path.Combine(GlobalConfig.TOOLS_BIN_DIR, repoName);
                    if (File.Exists(oldNoExtensionLink))
                    {
                        File.Delete(oldNoExtensionLink);
                        Logger.LogInfo($"Deleted old non-extension link '{oldNoExtensionLink}'.");
                    }
                    // Clean up old .exe extension executable/symlink if it existed
                    var oldExeLink = Path.Combine(GlobalConfig.TOOLS_BIN_DIR, repoName + ".exe");
                    if (File.Exists(oldExeLink))
                    {
                        File.Delete(oldExeLink);
                        Logger.LogInfo($"Deleted old .exe link '{oldExeLink}'.");
                    }

                    Logger.LogInfo($"Associated executable/symbolic link(s) for '{repoName}' removed.");
                }
                catch (Exception ex)
                {
                    Logger.LogWarn($"Failed to remove some executable/symbolic links for '{repoName}': {ex.Message}. Manual removal may be required. Please check permissions.");
                }
            }
            else // Unix-like systems
            {
                try
                {
                    Logger.LogInfo($"Removing executable/symbolic link: '{symlinkDestUnix}'...");
                    if (File.Exists(symlinkDestUnix))
                    {
                        File.Delete(symlinkDestUnix);
                        Logger.LogInfo($"Deleted '{symlinkDestUnix}'.");
                    }
                    // In Unix, if for some reason a .sh was explicitly created as a separate file
                    if (File.Exists(symlinkDestUnix + ".sh"))
                    {
                        File.Delete(symlinkDestUnix + ".sh");
                        Logger.LogInfo($"Deleted '{symlinkDestUnix}.sh'.");
                    }
                    Logger.LogInfo($"Executable/symbolic link '{symlinkDestUnix}' removed.");
                }
                catch (Exception ex)
                {
                    Logger.LogWarn($"Failed to remove executable/symbolic link '{symlinkDestUnix}': {ex.Message}. Manual removal may be required. Please check permissions.");
                }
            }


            // Remove project directory using the robust deletion method
            if (Directory.Exists(projectDir))
            {
                Logger.LogInfo($"Removing project directory: '{projectDir}'...");
                // Safety check for project_dir against TOOLS_BASE_DIR
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
        public static async Task SetupProject(string repoName, string githubUrl, bool forceCreateRunMode, string branch = "")
        {
            Logger.LogInfo($"Starting project setup script (Version: {GlobalConfig.SCRIPT_VERSION})...");

            await CheckSystemDependencies();

            var projectDir = Path.Combine(GlobalConfig.TOOLS_BASE_DIR, repoName);
            var venvDir = Path.Combine(projectDir, ".venv");
            var requirementsFile = Path.Combine(projectDir, "requirements.txt");

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
                var gitCloneEnv = new Dictionary<string, string?>
                {
                    { "HOME", Path.GetTempPath() }, // Use system temp path
                    { "GIT_ASKPASS", "" },
                    { "GIT_TERMINAL_PROMPT", "0" },
                    { "GITHUB_TOKEN", null },        // Unset sensitive token
                    { "GIT_SSH_COMMAND", null }      // Unset custom SSH command
                    // PATH is automatically inherited and not explicitly added here, relying on default behavior of ProcessStartInfo
                };
                string _b = branch.Length > 0 ? $"-b \"{branch}\"" : "";

                var (gitCloneExitCode, gitCloneOut, gitCloneErr) = await CommandExecutor.RunCommandAsync("git", $"clone {_b} \"{githubUrl}\" \"{projectDir}\"", Environment.CurrentDirectory, environmentVariables: gitCloneEnv);
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

            if (File.Exists(requirementsFile))
            {
                Logger.LogInfo($"Installing dependencies for '{repoName}' from '{requirementsFile}'...");
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
                    Logger.LogWarn($"Failed to install dependencies for '{repoName}': {pipErr}. Check '{requirementsFile}' and log for detailed pip errors.");
                }
                Logger.LogInfo($"Dependencies for '{repoName}' installed.");
            }
            else
            {
                Logger.LogWarn($"Neither 'build.sh' nor 'requirements.txt' found for '{repoName}'. Skipping dependency/build step.");
            }

            // --- Start of run script handling logic ---
            // On Windows, we'll create a .cmd wrapper.
            // On Unix-like, we'll create a single executable .sh script.

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

            string localProjectRunScriptPath = Path.Combine(projectDir, "run.sh");
            string localProjectMainPyPath = Path.Combine(projectDir, "main.py");


            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var targetCmdWrapperPath = Path.Combine(GlobalConfig.TOOLS_BIN_DIR, repoName + ".cmd");

                // Clean up any conflicting files/symlinks first
                if (File.Exists(targetCmdWrapperPath)) File.Delete(targetCmdWrapperPath);
                // Also clean up old .sh files or non-extension links from previous versions
                if (File.Exists(Path.Combine(GlobalConfig.TOOLS_BIN_DIR, repoName + ".sh"))) File.Delete(Path.Combine(GlobalConfig.TOOLS_BIN_DIR, repoName + ".sh"));
                if (File.Exists(Path.Combine(GlobalConfig.TOOLS_BIN_DIR, repoName))) File.Delete(Path.Combine(GlobalConfig.TOOLS_BIN_DIR, repoName));
                if (File.Exists(Path.Combine(GlobalConfig.TOOLS_BIN_DIR, repoName + ".exe"))) File.Delete(Path.Combine(GlobalConfig.TOOLS_BIN_DIR, repoName + ".exe"));


                // Determine the actual script to run for Windows (either project's run.sh or generated main.py wrapper)
                // If project has run.sh, we'll use that path for the cmd wrapper to call it via 'bash'
                // If not, we'll use localProjectMainPyPath
                string scriptToExecutePath;
                bool isProjectRunShUsed = false;
                if (File.Exists(localProjectRunScriptPath) && !forceCreateRunMode)
                {
                    scriptToExecutePath = localProjectRunScriptPath;
                    isProjectRunShUsed = true;
                    Logger.LogInfo($"'run.sh' found in project directory ('{localProjectRunScriptPath}'). Creating CMD wrapper to execute it via bash.");
                }
                else
                {
                    scriptToExecutePath = localProjectMainPyPath;
                    if (forceCreateRunMode)
                    {
                        Logger.LogInfo($"Force-creating default Python wrapper CMD script at '{targetCmdWrapperPath}' (ignoring existing project 'run.sh' if any).");
                    }
                    else
                    {
                        Logger.LogWarn($"No 'run.sh' found in project directory ('{localProjectRunScriptPath}'). Generating default Python wrapper CMD script directly at '{targetCmdWrapperPath}'.");
                    }

                    // For Windows, if we generate, we don't need a separate .sh file that gets called.
                    // The .cmd will directly activate venv and run Python.
                }

                // Generate the CMD wrapper content
                var cmdWrapperContent = new StringBuilder();
                cmdWrapperContent.AppendLine("@echo off");
                cmdWrapperContent.AppendLine($"rem This script was automatically generated by GitRepoPy (Version: {GlobalConfig.SCRIPT_VERSION}).");
                cmdWrapperContent.AppendLine($"rem It runs the Python application within its virtual environment.");
                cmdWrapperContent.AppendLine("");
                cmdWrapperContent.AppendLine($"set \"PROJECT_ROOT={projectDir}\"");
                cmdWrapperContent.AppendLine($"set \"VENV_DIR=%PROJECT_ROOT%\\.venv\"");
                cmdWrapperContent.AppendLine($"set \"VENV_ACTIVATE_BAT=%VENV_DIR%\\Scripts\\activate.bat\"");
                cmdWrapperContent.AppendLine("");
                cmdWrapperContent.AppendLine("if not exist \"%PROJECT_ROOT%\" (");
                cmdWrapperContent.AppendLine("    echo ERROR: Project directory '%PROJECT_ROOT%' not found or accessible.");
                cmdWrapperContent.AppendLine("    exit /b 1");
                cmdWrapperContent.AppendLine(")");
                cmdWrapperContent.AppendLine("");
                cmdWrapperContent.AppendLine("if not exist \"%VENV_ACTIVATE_BAT%\" (");
                cmdWrapperContent.AppendLine("    echo ERROR: Virtual environment activation script not found at '%VENV_ACTIVATE_BAT%'.");
                cmdWrapperContent.AppendLine("    echo Please ensure the virtual environment is correctly set up in '%VENV_DIR%'.");
                cmdWrapperContent.AppendLine("    exit /b 1");
                cmdWrapperContent.AppendLine(")");
                cmdWrapperContent.AppendLine("");
                cmdWrapperContent.AppendLine("rem Activate the virtual environment");
                cmdWrapperContent.AppendLine("call \"%VENV_ACTIVATE_BAT%\"");
                cmdWrapperContent.AppendLine("if %errorlevel% neq 0 (");
                cmdWrapperContent.AppendLine("    echo ERROR: Failed to activate virtual environment at '%VENV_ACTIVATE_BAT%'.");
                cmdWrapperContent.AppendLine("    exit /b 1");
                cmdWrapperContent.AppendLine(")");
                cmdWrapperContent.AppendLine("");

                if (isProjectRunShUsed)
                {
                    // If the project has its own run.sh, we still need bash.exe to execute it.
                    // The previous path conversion logic in FileSystemHelper is gone, so we must rely on bash's internal path handling or the user's PATH.
                    // This creates a circular dependency or reintroduces complexity.
                    // Therefore, for simplicity and eliminating bash.exe dependency,
                    // if a run.sh exists, we'll stick to generating a main.py wrapper as well (forceCreateRunMode behavior).
                    // Or, we explicitly state that 'run.sh' on Windows *must* be a batch file.
                    // Given the request to remove bash, the simplest is to always generate a Python call.
                    // Reverting strategy: If run.sh exists and it's Windows, we will NOT use it.
                    // The primary goal is removing bash.
                    // Let's modify the condition and log message to reflect this.
                    // The logic here is already inside the 'isProjectRunShUsed' block, implying we FOUND a run.sh.
                    // If we want to strictly remove bash, then we can't symlink to a .sh file for windows.
                    // The only path here for Windows should be to generate the Python call directly.
                    // Let's update the if/else to reflect this.
                    Logger.LogWarn($"Detected 'run.sh' in project for Windows, but for cross-platform compatibility without bash.exe, will generate a '.cmd' wrapper that executes 'main.py'.");
                    scriptToExecutePath = localProjectMainPyPath; // Override to main.py
                    // Continue with the Python execution below.
                }

                // Check if main.py exists and execute it
                cmdWrapperContent.AppendLine($"set \"MAIN_PYTHON_SCRIPT=%PROJECT_ROOT%\\main.py\"");
                cmdWrapperContent.AppendLine("if not exist \"%MAIN_PYTHON_SCRIPT%\" (");
                cmdWrapperContent.AppendLine("    echo ERROR: Main Python script '%MAIN_PYTHON_SCRIPT%' not found in project directory.");
                cmdWrapperContent.AppendLine("    echo Please ensure 'main.py' exists in '%PROJECT_ROOT%' or adjust the generated script.");
                cmdWrapperContent.AppendLine("    exit /b 1");
                cmdWrapperContent.AppendLine(")");
                cmdWrapperContent.AppendLine("");
                cmdWrapperContent.AppendLine("rem Execute the main Python script with all passed arguments");
                cmdWrapperContent.AppendLine("python \"%MAIN_PYTHON_SCRIPT%\" %*"); // %* passes all arguments
                cmdWrapperContent.AppendLine("set \"RUN_STATUS=%errorlevel%\"");
                cmdWrapperContent.AppendLine("");
                cmdWrapperContent.AppendLine("rem Deactivate the virtual environment (optional, as the batch file's env is local)");
                cmdWrapperContent.AppendLine("rem if defined VIRTUAL_ENV (call deactivate)"); // VIRTUAL_ENV is set by activate.bat
                cmdWrapperContent.AppendLine("");
                cmdWrapperContent.AppendLine("exit /b %RUN_STATUS%");


                try
                {
                    await File.WriteAllTextAsync(targetCmdWrapperPath, cmdWrapperContent.ToString());
                    Logger.LogInfo($"CMD wrapper script created at '{targetCmdWrapperPath}'.");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to create CMD wrapper script at '{targetCmdWrapperPath}': {ex.Message}. Please check permissions.", 1);
                }
                Logger.LogInfo($"Wrapper script for '{repoName}' created successfully at '{targetCmdWrapperPath}'.");
            }
            else // Linux/macOS (unchanged)
            {
                var targetBinExecutable = Path.Combine(GlobalConfig.TOOLS_BIN_DIR, repoName); // No extension for Unix-like
                // Clean up any conflicting files/symlinks first
                if (File.Exists(targetBinExecutable)) File.Delete(targetBinExecutable);
                if (File.Exists(targetBinExecutable + ".sh")) File.Delete(targetBinExecutable + ".sh"); // Clean up old .sh if it somehow exists

                if (File.Exists(localProjectRunScriptPath) && !forceCreateRunMode)
                {
                    // Scenario 1 (Unix-like): project_dir/run.sh found AND --force-create-run is NOT active
                    Logger.LogInfo($"'run.sh' found in project directory ('{localProjectRunScriptPath}'). Making it executable and creating a symbolic link.");
                    FileSystemHelper.SetExecutablePermissions(localProjectRunScriptPath);

                    FileSystemHelper.CreateSymlink(localProjectRunScriptPath, targetBinExecutable);
                }
                else
                {
                    // Scenario 2 (Unix-like): project_dir/run.sh NOT found OR --force-create-run IS active
                    if (forceCreateRunMode)
                    {
                        Logger.LogInfo($"Force-creating default Python wrapper script at '{targetBinExecutable}' (ignoring existing project 'run.sh' if any).");
                    }
                    else
                    {
                        Logger.LogWarn($"No 'run.sh' found in project directory ('{localProjectRunScriptPath}'). Generating default Python wrapper script directly at '{targetBinExecutable}'.");
                    }

                    // Generate the Bash script content directly into the targetBinExecutable file (no .sh extension)
                    var shScriptContent = new StringBuilder();
                    shScriptContent.AppendLine("#!/bin/bash");
                    shScriptContent.AppendLine($"# This script was automatically generated by GitRepoPy (Version: {GlobalConfig.SCRIPT_VERSION}).");
                    shScriptContent.AppendLine("# It acts as a wrapper to run the main Python application within its virtual environment.");
                    shScriptContent.AppendLine("");
                    shScriptContent.AppendLine($"PROJECT_ROOT=\"{projectDir}\"");
                    shScriptContent.AppendLine($"VENV_DIR=\"${{PROJECT_ROOT}}/.venv\"");
                    shScriptContent.AppendLine($"MAIN_PYTHON_SCRIPT=\"{localProjectMainPyPath}\"");
                    shScriptContent.AppendLine($"ACTIVATE_SCRIPT=\"${{VENV_DIR}}/bin/activate\""); // Unix-like venv path
                    shScriptContent.AppendLine("");
                    shScriptContent.AppendLine("# Basic checks");
                    shScriptContent.AppendLine("if [ ! -d \"$PROJECT_ROOT\" ]; then");
                    shScriptContent.AppendLine("    echo \"ERROR: Project directory '$PROJECT_ROOT' not found or accessible.\" >&2");
                    shScriptContent.AppendLine("    exit 1");
                    shScriptContent.AppendLine("fi");
                    shScriptContent.AppendLine("");
                    shScriptContent.AppendLine("if [ ! -f \"$MAIN_PYTHON_SCRIPT\" ]; then");
                    shScriptContent.AppendLine("    echo \"ERROR: Main Python script '$MAIN_PYTHON_SCRIPT' not found in project directory.\" >&2");
                    shScriptContent.AppendLine("    echo \"Please ensure 'main.py' exists in '$PROJECT_ROOT' or adjust the generated script.\" >&2");
                    shScriptContent.AppendLine("    exit 1");
                    shScriptContent.AppendLine("fi");
                    shScriptContent.AppendLine("");
                    shScriptContent.AppendLine("if [ ! -f \"$ACTIVATE_SCRIPT\" ]; then");
                    shScriptContent.AppendLine("    echo \"ERROR: Virtual environment activation script not found at '$ACTIVATE_SCRIPT'.\" >&2");
                    shScriptContent.AppendLine("    echo \"Please ensure the virtual environment is correctly set up in '$VENV_DIR'.\" >&2");
                    shScriptContent.AppendLine("    exit 1");
                    shScriptContent.AppendLine("fi");
                    shScriptContent.AppendLine("");
                    shScriptContent.AppendLine("# Activate the virtual environment");
                    shScriptContent.AppendLine("source \"$ACTIVATE_SCRIPT\"");
                    shScriptContent.AppendLine("if [ $? -ne 0 ]; then");
                    shScriptContent.AppendLine("    echo \"ERROR: Failed to activate virtual environment at '$ACTIVATE_SCRIPT'.\" >&2");
                    shScriptContent.AppendLine("    exit 1");
                    shScriptContent.AppendLine("fi");
                    shScriptContent.AppendLine("");
                    shScriptContent.AppendLine("# Execute the main Python script with all passed arguments");
                    shScriptContent.AppendLine("python \"$MAIN_PYTHON_SCRIPT\" \"$@\"");
                    shScriptContent.AppendLine("RUN_STATUS=$?");
                    shScriptContent.AppendLine("");
                    shScriptContent.AppendLine("# Deactivate the virtual environment if the function exists");
                    shScriptContent.AppendLine("if declare -f deactivate &>/dev/null; then");
                    shScriptContent.AppendLine("    deactivate");
                    shScriptContent.AppendLine("fi");
                    shScriptContent.AppendLine("");
                    shScriptContent.AppendLine("exit $RUN_STATUS");

                    try
                    {
                        await File.WriteAllTextAsync(targetBinExecutable, shScriptContent.ToString());
                        Logger.LogInfo($"Wrapper script created at '{targetBinExecutable}'.");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to create wrapper script at '{targetBinExecutable}': {ex.Message}. Please check permissions.", 1);
                    }

                    FileSystemHelper.SetExecutablePermissions(targetBinExecutable); // Make the file executable
                }
                Logger.LogInfo($"Wrapper script for '{repoName}' created successfully at '{targetBinExecutable}'.");
            }
            // --- End of run script handling logic ---

            // After successful setup and run script creation, add to PATH
            PathEnvironmentManager.AddToolsBinToSystemPath();

            Logger.LogInfo($"Project setup for '{repoName}' completed successfully!");
        }
    }
}
