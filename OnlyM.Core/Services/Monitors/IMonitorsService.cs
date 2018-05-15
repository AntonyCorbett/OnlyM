namespace OnlyM.Core.Services.Monitors
{
    using System;
    using System.Collections.Generic;
    using Models;

    public interface IMonitorsService
    {
        IEnumerable<SystemMonitor> GetSystemMonitors();

        SystemMonitor GetSystemMonitor(string monitorId);

        SystemMonitor GetMonitorForWindowHandle(IntPtr windowHandle);
    }
}
