using System.IO;

namespace PasteTool.Core.Services;

public sealed class FileLogger : ILogger
{
    private readonly string _logFilePath;
    private readonly object _lock = new();

    public FileLogger(string logDirectory)
    {
        Directory.CreateDirectory(logDirectory);
        var timestamp = DateTime.Now.ToString("yyyyMMdd");
        _logFilePath = Path.Combine(logDirectory, $"pastetool_{timestamp}.log");
    }

    public void LogInfo(string message)
    {
        WriteLog("INFO", message, null);
    }

    public void LogWarning(string message, Exception? exception = null)
    {
        WriteLog("WARN", message, exception);
    }

    public void LogError(string message, Exception? exception = null)
    {
        WriteLog("ERROR", message, exception);
    }

    private void WriteLog(string level, string message, Exception? exception)
    {
        try
        {
            lock (_lock)
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logEntry = $"[{timestamp}] [{level}] {message}";

                if (exception != null)
                {
                    logEntry += $"\n  Exception: {exception.GetType().Name}: {exception.Message}";
                    logEntry += $"\n  StackTrace: {exception.StackTrace}";
                }

                File.AppendAllText(_logFilePath, logEntry + "\n");
            }
        }
        catch
        {
            // Silently fail if logging fails to avoid cascading errors
        }
    }
}
