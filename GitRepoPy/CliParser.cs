using System;
using System.Linq;
using GitRepoPy; // For Logger and GlobalConfig

namespace GitRepoPy
{
    /// <summary>
    /// Defines the different operational modes of the script.
    /// </summary>
    public enum ScriptMode
    {
        Setup,
        Remove,
        BuildPythonRun, // Legacy mode, now falls into Setup with specific force-run behavior
        ForceCreateRun,
        Update,
        Help,
        List
    }

    /// <summary>
    /// Holds the parsed command-line arguments.
    /// </summary>
    public struct ParsedArgs
    {
        public ParsedArgs(){}

        public ScriptMode Mode { get; set; }
        public bool Verbose { get; set; }
        public string RepoName { get; set; } = "";
        public string GitHubUrl { get; set; } = "";
        public string Branch { get; set; } = "";
    }

    /// <summary>
    /// Handles parsing and validation of command-line arguments.
    /// </summary>
    public static class CliParser
    {
        /// <summary>
        /// Parses command-line arguments and determines the script's operating mode.
        /// </summary>
        /// <param name="args">The command-line arguments array.</param>
        /// <returns>A ParsedArgs struct containing the determined mode and relevant arguments.</returns>
        public static ParsedArgs Parse(string[] args)
        {
            var parsed = new ParsedArgs { Mode = ScriptMode.Setup }; // Default mode

            if (args.Length == 0)
            {
                Logger.LogError("No arguments provided. Use --help for usage.", 1);
                return parsed;
            }

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (arg.StartsWith("-"))
                {
                    switch (arg)
                    {
                        case "--verbose":
                        case "-v":
                            parsed.Verbose = true;
                            break;
                        case "--list":
                        case "-l":
                            parsed.Mode = ScriptMode.List;
                            return parsed;
                        case "--branch":
                        case "-b":
                            if (++i < args.Length) parsed.Branch = args[i];
                            break;

                        case "--remove":
                        case "-r":
                            parsed.Mode = ScriptMode.Remove;
                            if (++i < args.Length) parsed.RepoName = args[i];
                            break;
                        case "--build-python-run":
                        case "-bpr":
                            parsed.Mode = ScriptMode.BuildPythonRun;
                            if (++i < args.Length) parsed.RepoName = args[i];
                            if (++i < args.Length) parsed.GitHubUrl = args[i];
                            break;
                        case "--force-create-run":
                        case "-fcr":
                            parsed.Mode = ScriptMode.ForceCreateRun;
                            if (++i < args.Length) parsed.RepoName = args[i];
                            if (++i < args.Length) parsed.GitHubUrl = args[i];
                            break;
                        case "--update":
                        case "-u":
                            parsed.Mode = ScriptMode.Update;
                            if (++i < args.Length) parsed.RepoName = args[i];
                            break;
                        case "--help":
                        case "-h":
                            parsed.Mode = ScriptMode.Help;
                            return parsed; // Help mode means we're done parsing and will just display help
                        default:
                            Logger.LogWarn($"Unknown option: {arg}. Ignoring.");
                            break;
                    }
                }
                else
                {
                    // Positional arguments for default setup mode
                    if (parsed.Mode == ScriptMode.Setup)
                    {
                        if (string.IsNullOrEmpty(parsed.RepoName))
                        {
                            parsed.RepoName = arg;
                        }
                        else if (string.IsNullOrEmpty(parsed.GitHubUrl))
                        {
                            parsed.GitHubUrl = arg;
                        }
                        else
                        {
                            Logger.LogWarn($"Unexpected argument: {arg}. Ignoring.");
                        }
                    }
                    else
                    {
                        Logger.LogWarn($"Unexpected positional argument '{arg}' after an option. Ignoring.");
                    }
                }
            }

            // Post-parsing validation for required arguments
            switch (parsed.Mode)
            {
                case ScriptMode.Remove:
                case ScriptMode.Update:
                    if (string.IsNullOrEmpty(parsed.RepoName))
                    {
                        Logger.LogError($"Usage for {parsed.Mode.ToString().ToLower()}: {AppDomain.CurrentDomain.FriendlyName} --{parsed.Mode.ToString().ToLower().Replace("buildpythonrun", "build-python-run").Replace("forcecreaterun", "force-create-run")} <repository_name>\nExample: {AppDomain.CurrentDomain.FriendlyName} --{parsed.Mode.ToString().ToLower().Replace("buildpythonrun", "build-python-run").Replace("forcecreaterun", "force-create-run")} my-web-app");
                    }
                    break;
                case ScriptMode.Setup:
                case ScriptMode.BuildPythonRun:
                case ScriptMode.ForceCreateRun:
                    if (string.IsNullOrEmpty(parsed.RepoName) || string.IsNullOrEmpty(parsed.GitHubUrl))
                    {
                        Logger.LogError($"Usage for setup modes ({parsed.Mode.ToString().ToLower().Replace("buildpythonrun", "build-python-run").Replace("forcecreaterun", "force-create-run")} or default): {AppDomain.CurrentDomain.FriendlyName} <option> <repository_name> <github_url>\nExample: {AppDomain.CurrentDomain.FriendlyName} my-web-app https://github.com/myuser/my-web-app.git");
                    }
                    break;
            }

            return parsed;
        }

        /// <summary>Displays the help message, similar to the bash script's --help output.</summary>
        public static void DisplayHelp()
        {
            // Note: CheckSystemDependencies call moved to ProjectService.CheckSystemDependencies,
            // as this method is purely for displaying CLI usage.
            var appName = AppDomain.CurrentDomain.FriendlyName; // Get executable name

            Console.WriteLine($"{GlobalConfig.INFO_COLOR}Usage: {appName} [OPTIONS] <repository_name> [github_url]{GlobalConfig.RESET_COLOR}");
            Console.WriteLine("\nThis script automates the setup, removal, and updating of Python projects from Git repositories.");
            Console.WriteLine($"\n{GlobalConfig.WARN_COLOR}Options:{GlobalConfig.RESET_COLOR}");
            Console.WriteLine($"  -r, --remove <repository_name>          : Deletes the symbolic link and the entire project directory.");
            Console.WriteLine($"                                            Example: {appName} --remove my-web-app");
            Console.WriteLine($"\n  -bpr, --build-python-run <repo_name> <url> : Clones, sets up venv, installs dependencies.");
            Console.WriteLine($"                                            If 'run.sh' not found in project, generates a default");
            Console.WriteLine($"                                            Python run script in {GlobalConfig.TOOLS_BIN_DIR}.");
            Console.WriteLine($"                                            Example: {appName} --build-python-run my-app https://github.com/user/my-app.git");
            Console.WriteLine($"\n  -fcr, --force-create-run <repo_name> <url> : Similar to default setup, but always generates");
            Console.WriteLine($"                                            the default Python run script directly in {GlobalConfig.TOOLS_BIN_DIR},");
            Console.WriteLine($"                                            overriding any existing 'run.sh' in the project.");
            Console.WriteLine($"                                            Example: {appName} --force-create-run my-app https://github.com/user/my-app.git");
            Console.WriteLine($"\n  -u, --update <repository_name>          : Cleans __pycache__, stashes local changes, pulls latest");
            Console.WriteLine($"                                            from remote, and pops stashed changes.");
            Console.WriteLine($"                                            Example: {appName} --update my-web-app");
            Console.WriteLine($"\n  -h, --help                            : Displays this help message and exits.");
            Console.WriteLine($"\n{GlobalConfig.INFO_COLOR}Default Setup Mode (no specific option):{GlobalConfig.RESET_COLOR}");
            // Corrected line for proper alignment
            Console.WriteLine($"  {appName} <repository_name> <github_url>");
            Console.WriteLine($"  Example: {appName} my-web-app https://github.com/myuser/my-web-app.git");
            Console.WriteLine($"\n{GlobalConfig.INFO_COLOR}Version:{GlobalConfig.RESET_COLOR} {GlobalConfig.SCRIPT_VERSION}");
            Environment.Exit(0);
        }
    }
}
