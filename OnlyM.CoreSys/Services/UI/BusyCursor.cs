using System.Windows;

namespace OnlyM.CoreSys.Services.UI
{
    using System;
    using System.Threading;
    using System.Windows.Input;

    public sealed class BusyCursor : IDisposable
    {
        private static int busyCount;
        
        public BusyCursor()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Interlocked.Increment(ref busyCount);
                StatusChangedEvent?.Invoke(this, EventArgs.Empty);
                Mouse.OverrideCursor = Cursors.Wait;
            });
        }

        public static event EventHandler StatusChangedEvent;

        public static bool IsBusy()
        {
            return busyCount > 0;
        }

        public void Dispose()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Interlocked.Decrement(ref busyCount);
                StatusChangedEvent?.Invoke(this, EventArgs.Empty);

                if (busyCount == 0)
                {
                    Mouse.OverrideCursor = null;
                }
            });
        }
    }
}
