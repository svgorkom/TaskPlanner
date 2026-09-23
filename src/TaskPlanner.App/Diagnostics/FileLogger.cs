using System.IO;

namespace TaskPlanner.App.Diagnostics;

/// <summary>
/// Minimal, dependency-free file logger used for startup/runtime diagnostics
/// (global hotkey activation, unhandled exceptions, etc.).
/// </summary>
internal static class FileLogger
{
    private static readonly object SyncRoot = new();
    private static string logFilePath = string.Empty;

    public static void Initialize(string appDataPath)
    {
        Directory.CreateDirectory(appDataPath);
        logFilePath = Path.Combine(appDataPath, "app.log");
    }

    public static void Log(string message)
    {
        if (string.IsNullOrEmpty(logFilePath))
        {
            return;
        }

        try
        {
            lock (SyncRoot)
            {
                File.AppendAllText(logFilePath, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never crash the application.
        }
    }

    public static void LogException(string context, Exception exception)
    {
        Log($"{context}: {exception}");
    }
}
