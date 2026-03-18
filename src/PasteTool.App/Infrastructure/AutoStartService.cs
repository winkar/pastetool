using Microsoft.Win32;

namespace PasteTool.App.Infrastructure;

public sealed class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "PasteTool";

    public void Apply(bool enabled, string executablePath, Action<string>? onError = null)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key is null)
            {
                onError?.Invoke("无法访问注册表");
                return;
            }

            if (enabled)
            {
                key.SetValue(ValueName, $"\"{executablePath}\"");
            }
            else
            {
                key.DeleteValue(ValueName, false);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            onError?.Invoke($"没有权限修改注册表: {ex.Message}");
        }
        catch (Exception ex)
        {
            onError?.Invoke($"设置开机启动失败: {ex.Message}");
        }
    }
}
