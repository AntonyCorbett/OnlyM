namespace OnlyM.Core.Services.Monitors
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Forms;
    using Models;
    using Serilog;

    /// <summary>
    /// Service to get display device information
    /// </summary>
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class MonitorsService : IMonitorsService
    {
        /// <summary>
        /// Gets a collection of system monitors
        /// </summary>
        /// <returns>Collection of SystemMonitor</returns>
        public IEnumerable<SystemMonitor> GetSystemMonitors()
        {
            Log.Logger.Debug("Getting system monitors");
            
            List<SystemMonitor> result = new List<SystemMonitor>();

            var devices = DisplayDevices.ReadDisplayDevices().ToArray();

            var displayScreens = GetDisplayScreens(devices);
            
            foreach (var screen in Screen.AllScreens)
            {
                var displayScreen = displayScreens?.SingleOrDefault(x => x.Item1.Equals(screen));
                var deviceData = displayScreen?.Item2;

                var monitor = new SystemMonitor
                {
                    Monitor = screen,
                    MonitorName = deviceData?.DeviceString ?? SanitizeScreenDeviceName(screen.DeviceName),
                    MonitorId = deviceData?.DeviceId ?? screen.DeviceName,
                    FriendlyName = screen.DeviceFriendlyName()
                };

                if (string.IsNullOrEmpty(monitor.FriendlyName))
                {
                    monitor.FriendlyName = monitor.MonitorName;
                }

                result.Add(monitor);
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

        private string SanitizeScreenDeviceName(string name)
        {
            return name.Replace(@"\\.\", string.Empty);
        }

        private List<(Screen, DisplayDeviceData)> GetDisplayScreens(DisplayDeviceData[] devices)
        {
            var result = new List<(Screen, DisplayDeviceData)>();

            foreach (var screen in Screen.AllScreens)
            {
                Log.Logger.Verbose($"Screen: {screen.DeviceName}");

                var deviceData = GetDeviceMatchingScreen(devices, screen);
                if (deviceData == null)
                {
                    return null;
                }

                Log.Logger.Verbose($"Matching device: {deviceData.DeviceString}, {deviceData.DeviceId}");
                result.Add((screen, deviceData));
            }

            return result;
        }
    }
}
