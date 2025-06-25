using System;
using System.IO;
using System.Runtime.InteropServices;

namespace GitRepoPy
{
    /// <summary>
    /// Centralizes global constants and configuration settings for the application.
    /// Includes OS-specific path defaults and console color definitions.
    /// Also contains P/Invoke declarations for Windows console color support.
    /// </summary>
    public static class GlobalConfig
    {
        public const string SCRIPT_VERSION = "1.9.0";

        // Base directory for cloned repositories
        // Falls back to /usr/local/tools on Unix-like.
        // On Windows, it defaults to C:\Users\<Username>\Tools if env var not set.
        public static readonly string TOOLS_BASE_DIR = Environment.GetEnvironmentVariable("TOOLS_BASE_DIR") ??
            (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Tools") : "/usr/local/tools");

        // Directory where project executables/symlinks will be placed
        // Falls back to /usr/local/bin on Unix-like.
        // On Windows, it defaults to C:\Users\<Username>\Tools\Bin if env var not set.
        public static readonly string TOOLS_BIN_DIR = Environment.GetEnvironmentVariable("TOOLS_BIN_DIR") ??
            (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Tools", "Bin") : "/usr/local/bin");

        // ANSI escape codes for console colors
        public const string INFO_COLOR = "\x1b[32m";  // Green
        public const string WARN_COLOR = "\x1b[33m";  // Yellow
        public const string ERROR_COLOR = "\x1b[31m"; // Red
        public const string RESET_COLOR = "\x1b[0m"; // Reset to default

        // --- P/Invoke for Windows Console Color Support ---
        // Required to enable ANSI escape sequences on Windows 10+ command prompt
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        public const int STD_OUTPUT_HANDLE = -11;
        public const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

        /// <summary>
        /// Initializes console for ANSI escape sequences on Windows.
        /// This method should ideally be called once at application startup.
        /// </summary>
        internal static void InitializeConsole()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    var handle = GetStdHandle(STD_OUTPUT_HANDLE);
                    GetConsoleMode(handle, out uint mode);
                    SetConsoleMode(handle, mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING);
                }
                catch { /* Ignore if it fails - older Windows versions or non-compatible terminals */ }
            }
        }
    }
}
