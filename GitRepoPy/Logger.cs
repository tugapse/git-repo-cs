
namespace GitRepoPy
{
    /// <summary>
    /// Handles all console logging operations with colored output.
    /// </summary>
    public static class Logger
    {
        // Static constructor to ensure console is initialized for colors once.
        static Logger()
        {
            GlobalConfig.InitializeConsole();
        }

        /// <summary>Logs an informational message to the console.</summary>
        /// <param name="message">The message to log.</param>
        public static void LogInfo(string message, string color = "" ) => Console.WriteLine($"{color}[INFO] {DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{GlobalConfig.RESET_COLOR}");

        /// <summary>Logs a warning message to the console (to standard error stream).</summary>
        /// <param name="message">The message to log.</param>
        public static void LogWarn(string message)
        {
            if (Program.Verbose)
                Console.Error.WriteLine($"{GlobalConfig.WARN_COLOR}[WARN] {DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{GlobalConfig.RESET_COLOR}");
        }

        /// <summary>Logs an error message to the console (to standard error stream) and exits the application.</summary>
        /// <param name="message">The error message to log.</param>
        /// <param name="exitCode">The exit code to use when exiting the application. Defaults to 1.</param>
        public static void LogError(string message, int exitCode = 1)
        {
            Console.Error.WriteLine($"{GlobalConfig.ERROR_COLOR}[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{GlobalConfig.RESET_COLOR}");
            Environment.Exit(exitCode);
        }
    }
}
