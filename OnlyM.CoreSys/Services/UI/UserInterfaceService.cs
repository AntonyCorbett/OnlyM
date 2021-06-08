using System;

namespace OnlyM.CoreSys.Services.UI
{ 
    public class UserInterfaceService : IUserInterfaceService
    {
        public UserInterfaceService()
        {
            BusyCursor.StatusChangedEvent += HandleBusyStatusChangedEvent;
        }

        public event EventHandler? BusyStatusChangedEvent;

        public BusyCursor BeginBusy() => new();

        public bool IsBusy() => BusyCursor.IsBusy();
        
        private void HandleBusyStatusChangedEvent(object? sender, EventArgs e) 
            => BusyStatusChangedEvent?.Invoke(this, EventArgs.Empty);
    }
}
