using System;

namespace OnlyM.CoreSys.Services.UI;

public interface IUserInterfaceService
{
    event EventHandler BusyStatusChangedEvent;

    BusyCursor BeginBusy();

    bool IsBusy();
}
