using System.Drawing;
using System.IO;
using System.Reflection;
using Forms = System.Windows.Forms;

namespace PasteTool.App.Infrastructure;

public sealed class TrayIconHost : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;

    public TrayIconHost(Action showHistory, Action showSettings, Func<Task> clearHistoryAsync, Action exit)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开历史", null, (_, _) => showHistory());
        menu.Items.Add("设置", null, (_, _) => showSettings());
        menu.Items.Add("清空历史", null, async (_, _) => await clearHistoryAsync());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => exit());

        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "PasteTool",
            Icon = LoadCustomIcon() ?? SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = menu,
        };

        _notifyIcon.DoubleClick += (_, _) => showHistory();
    }

    private static Icon? LoadCustomIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico");
            if (File.Exists(iconPath))
            {
                return new Icon(iconPath);
            }

            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "PasteTool.App.Resources.app.ico";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                return new Icon(stream);
            }
        }
        catch
        {
            // Fall back to system icon
        }

        return null;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
