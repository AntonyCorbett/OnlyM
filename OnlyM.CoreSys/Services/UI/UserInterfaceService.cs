namespace OnlyM.CoreSys.Services.UI
{
    using System;

    public class UserInterfaceService : IUserInterfaceService
    {
        public UserInterfaceService()
        {
            BusyCursor.StatusChangedEvent += HandleBusyStatusChangedEvent;
        }

        public event EventHandler BusyStatusChangedEvent;

        public BusyCursor BeginBusy()
        {
            return new BusyCursor();
        }

        public bool IsBusy()
        {
            return BusyCursor.IsBusy();
        }

        private void HandleBusyStatusChangedEvent(object sender, System.EventArgs e)
        {
            BusyStatusChangedEvent?.Invoke(this, EventArgs.Empty);
        }
    }
}
