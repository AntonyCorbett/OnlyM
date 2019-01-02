namespace OnlyM.CoreSys.Services.UI
{
    using System;
    using System.Threading;
    using System.Windows.Input;
    using GalaSoft.MvvmLight.Threading;

    public sealed class BusyCursor : IDisposable
    {
        private static int busyCount;

        private Cursor _originalCursor;
        
        public BusyCursor()
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                Interlocked.Increment(ref busyCount);
                StatusChangedEvent?.Invoke(this, EventArgs.Empty);
                _originalCursor = Mouse.OverrideCursor;
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
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                Interlocked.Decrement(ref busyCount);
                StatusChangedEvent?.Invoke(this, EventArgs.Empty);
                Mouse.OverrideCursor = _originalCursor; 
            });
        }
    }
}
