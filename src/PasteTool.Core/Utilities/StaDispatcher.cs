using System.Windows.Threading;

namespace PasteTool.Core.Utilities;

public sealed class StaDispatcher : IDisposable
{
    private readonly TaskCompletionSource<Dispatcher> _dispatcherSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Thread _thread;
    private bool _disposed;

    public StaDispatcher(string name)
    {
        _thread = new Thread(ThreadStart)
        {
            IsBackground = true,
            Name = name,
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public void Invoke(Action action)
    {
        Dispatcher.Invoke(action);
    }

    public T Invoke<T>(Func<T> action)
    {
        return Dispatcher.Invoke(action);
    }

    public Task InvokeAsync(Action action)
    {
        return Dispatcher.InvokeAsync(action).Task;
    }

    public Task<T> InvokeAsync<T>(Func<T> action)
    {
        return Dispatcher.InvokeAsync(action).Task;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_dispatcherSource.Task.IsCompleted)
        {
            var dispatcher = _dispatcherSource.Task.Result;
            dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);

            // Wait for thread to finish with timeout
            if (!_thread.Join(TimeSpan.FromSeconds(5)))
            {
                // Thread didn't finish gracefully, but we tried
            }
        }
    }

    private Dispatcher Dispatcher => _dispatcherSource.Task.GetAwaiter().GetResult();

    private void ThreadStart()
    {
        _dispatcherSource.TrySetResult(Dispatcher.CurrentDispatcher);
        Dispatcher.Run();
    }
}
