using System;

namespace OnlyM.Core.Services.Monitors
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Forms;
    using Models;
    using Serilog;

    /// <summary>
    /// Service to get display device information
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public sealed class MonitorsService : IMonitorsService
    {
        /// <summary>
        /// Gets a collection of system monitors
        /// </summary>
        /// <returns>Collection of SystemMonitor</returns>
        public IEnumerable<SystemMonitor> GetSystemMonitors()
        {
            Log.Logger.Information("Getting system monitors");
            
            List<SystemMonitor> result = new List<SystemMonitor>();

            var devices = DisplayDevices.ReadDisplayDevices().ToArray();

            foreach (var screen in Screen.AllScreens)
            {
                Log.Logger.Information($"Screen: {screen.DeviceName}");
                
                DisplayDeviceData deviceData = GetDeviceMatchingScreen(devices, screen);
                if (deviceData != null)
                {
                    Log.Logger.Information($"Matching device: {deviceData.DeviceString}, {deviceData.DeviceId}");
                    
                    result.Add(new SystemMonitor
                    {
                        Monitor = screen,
                        MonitorName = deviceData.DeviceString,
                        MonitorId = deviceData.DeviceId
                    });
                }
            }

            return result;
        }

        public SystemMonitor GetSystemMonitor(string monitorId)
        {
            return GetSystemMonitors().SingleOrDefault(x => x.MonitorId.Equals(monitorId));
        }

        public SystemMonitor GetMonitorForWindowHandle(IntPtr windowHandle)
        {
            var screen = Screen.FromHandle(windowHandle);
            return GetSystemMonitors().SingleOrDefault(x => x.Monitor.DeviceName.Equals(screen.DeviceName));
        }

        private DisplayDeviceData GetDeviceMatchingScreen(DisplayDeviceData[] devices, Screen screen)
        {
            var deviceName = screen.DeviceName + "\\";
            return devices.SingleOrDefault(x => x.Name.StartsWith(deviceName));
        }
    }
}
