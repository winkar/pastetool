using PasteTool.Core.Services;

namespace PasteTool.Core.Tests;

internal sealed class TestLogger : ILogger
{
    public void LogInfo(string message)
    {
    }

    public void LogWarning(string message, Exception? exception = null)
    {
    }

    public void LogError(string message, Exception? exception = null)
    {
    }
}
