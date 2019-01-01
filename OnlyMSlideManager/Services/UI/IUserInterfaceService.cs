namespace OnlyMSlideManager.Services.UI
{
    using System;

    public interface IUserInterfaceService
    {
        event EventHandler BusyStatusChangedEvent;

        BusyCursor BeginBusy();

        bool IsBusy();
    }
}
