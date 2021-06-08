using System;
using System.Collections.Generic;
using OnlyM.Core.Models;

namespace OnlyM.Core.Services.Monitors
{
    public interface IMonitorsService
    {
        IEnumerable<SystemMonitor> GetSystemMonitors();

        SystemMonitor? GetSystemMonitor(string monitorId);

        SystemMonitor? GetMonitorForWindowHandle(IntPtr windowHandle);
    }
}
