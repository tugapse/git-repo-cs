using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GitRepoPy
{
    /// <summary>
    /// Provides functionality to execute external shell commands.
    /// </summary>
    public static class CommandExecutor
    {
        /// <summary>
        /// Runs an external shell command and captures its output.
        /// </summary>
        /// <param name="command">The command to execute (e.g., "git", "python3").</param>
        /// <param name="args">The arguments for the command.</param>
        /// <param name="workingDirectory">The directory in which to execute the command. Defaults to current directory.</param>
        /// <param name="logOutput">If true, logs stdout and stderr of the command.</param>
        /// <param name="environmentVariables">Optional dictionary of environment variables to set/unset for the process.
        ///                                     If a value is null, the variable is removed. Otherwise, it's set.</param>
        /// <returns>A tuple containing the exit code, standard output, and standard error of the command.</returns>
        public static async Task<(int ExitCode, string StdOut, string StdErr)> RunCommandAsync(
            string command,
            string args,
            string? workingDirectory = null, // Mark as nullable
            bool logOutput = true,
            IDictionary<string, string?>? environmentVariables = null) // Mark values and dictionary as nullable
        {
            Logger.LogInfo($"Executing: {command} {args}");
            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false, // Must be false to redirect streams
                CreateNoWindow = true,   // Don't create a new window for the process
                WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory
            };

            // If specific environment variables are provided, modify the inherited environment.
            if (environmentVariables != null)
            {
                foreach (var kvp in environmentVariables)
                {
                    if (kvp.Value == null)
                    {
                        // If value is null, remove the variable from the environment
                        if (startInfo.EnvironmentVariables.ContainsKey(kvp.Key))
                        {
                            startInfo.EnvironmentVariables.Remove(kvp.Key);
                        }
                    }
                    else
                    {
                        // Otherwise, set or update the variable
                        startInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
                    }
                }
            }

            using var process = new Process { StartInfo = startInfo };

            try
            {
                process.Start();
                string stdOut = await process.StandardOutput.ReadToEndAsync();
                string stdErr = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (logOutput)
                {
                    if (!string.IsNullOrWhiteSpace(stdOut)) Logger.LogInfo($"STDOUT:\n{stdOut}");
                    if (!string.IsNullOrWhiteSpace(stdErr)) Logger.LogWarn($"STDERR:\n{stdErr}");
                }

                return (process.ExitCode, stdOut, stdErr);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to execute command '{command} {args}': {ex.Message}", 1);
                return (-1, string.Empty, ex.Message); // Return -1 for execution failure
            }
        }
    }
}
