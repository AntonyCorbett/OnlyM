using System.Windows;
using System;
using System.Threading;
using System.Windows.Input;

namespace OnlyM.CoreSys.Services.UI
{
    public sealed class BusyCursor : IDisposable
    {
        private static int BusyCount;
        
        public BusyCursor()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Interlocked.Increment(ref BusyCount);
                StatusChangedEvent?.Invoke(null, EventArgs.Empty);
                Mouse.OverrideCursor = Cursors.Wait;
            });
        }

        public static event EventHandler StatusChangedEvent;

        public static bool IsBusy() => BusyCount > 0;
        
        public void Dispose()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Interlocked.Decrement(ref BusyCount);
                StatusChangedEvent?.Invoke(null, EventArgs.Empty);

                if (BusyCount == 0)
                {
                    Mouse.OverrideCursor = null;
                }
            });
        }
    }
}
