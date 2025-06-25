using System;
using System.Threading.Tasks;

// Using the namespace for our new organized classes
using GitRepoPy;

/// <summary>
/// The main entry point for the GitRepoPy application.
/// Orchestrates command-line argument parsing and delegates tasks
/// to respective service classes.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        // Logger's static constructor handles console initialization for colors
        Logger.LogInfo($"Starting script (Version: {GlobalConfig.SCRIPT_VERSION})...");

        // Parse command-line arguments
        var parsedArgs = CliParser.Parse(args);

        // Execute logic based on the determined script mode
        switch (parsedArgs.Mode)
        {
            case ScriptMode.Help:
                CliParser.DisplayHelp(); // Exits internally after display
                break;
            case ScriptMode.Remove:
                ProjectService.RemoveProject(parsedArgs.RepoName); // Exits internally after completion
                break;
            case ScriptMode.Update:
                await ProjectService.UpdateProject(parsedArgs.RepoName); // Exits internally after completion
                break;
            case ScriptMode.Setup:
            case ScriptMode.BuildPythonRun:
            case ScriptMode.ForceCreateRun:
                // All setup-like modes funnel into this internal method
                await ProjectService.SetupProject(parsedArgs.RepoName, parsedArgs.GitHubUrl, parsedArgs.Mode == ScriptMode.ForceCreateRun);
                break;
            default:
                Logger.LogError("Invalid script mode detected.", 1);
                break;
        }
    }
}
