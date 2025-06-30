using System;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace OnlyM.CoreSys.Services.UI;

public sealed class BusyCursor : IDisposable
{
    private static int _busyCount;

    public BusyCursor()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Interlocked.Increment(ref _busyCount);
            StatusChangedEvent?.Invoke(null, EventArgs.Empty);
            Mouse.OverrideCursor = Cursors.Wait;
        });
    }

    public static event EventHandler? StatusChangedEvent;

    public static bool IsBusy() => _busyCount > 0;

    public void Dispose() =>
        Application.Current.Dispatcher.Invoke(() =>
        {
            Interlocked.Decrement(ref _busyCount);
            StatusChangedEvent?.Invoke(null, EventArgs.Empty);

            if (_busyCount == 0)
            {
                Mouse.OverrideCursor = null;
            }
        });
}
