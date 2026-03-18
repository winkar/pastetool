using System.Threading;
using System.Windows;
using Application = System.Windows.Application;

namespace PasteTool.App;

public partial class App : Application
{
    private Mutex? _singleInstanceMutex;
    private AppController? _controller;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(true, "PasteTool.Singleton", out var createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        try
        {
            _controller = new AppController(this);
            await _controller.StartAsync();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"启动 PasteTool 失败:\n{ex.Message}",
                "PasteTool",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _controller?.Dispose();
        }
        finally
        {
            _singleInstanceMutex?.Dispose();
            base.OnExit(e);
        }
    }
}
