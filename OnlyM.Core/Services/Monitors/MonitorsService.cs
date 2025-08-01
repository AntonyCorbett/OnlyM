﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using OnlyM.Core.Models;
using Serilog;

namespace OnlyM.Core.Services.Monitors;

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

        var result = new List<SystemMonitor>();

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
                FriendlyName = screen.DeviceFriendlyName(),
            };

            if (string.IsNullOrEmpty(monitor.FriendlyName))
            {
                monitor.FriendlyName = monitor.MonitorName;
            }

            result.Add(monitor);
        }

        return result;
    }

    public SystemMonitor? GetSystemMonitor(string? monitorId) =>
        GetSystemMonitors().SingleOrDefault(
            x => x.MonitorId != null &&
                 x.MonitorId.Equals(monitorId, StringComparison.Ordinal));

    public SystemMonitor? GetMonitorForWindowHandle(IntPtr windowHandle)
    {
#pragma warning disable U2U1212
        var screen = Screen.FromHandle(windowHandle);
        return GetSystemMonitors().SingleOrDefault(
            x => x.Monitor?.DeviceName != null &&
                 x.Monitor.DeviceName.Equals(screen.DeviceName, StringComparison.Ordinal));
#pragma warning restore U2U1212
    }

    private static DisplayDeviceData? GetDeviceMatchingScreen(DisplayDeviceData[] devices, Screen screen)
    {
        var deviceName = screen.DeviceName + "\\";
        return devices.SingleOrDefault(x => x.Name != null && x.Name.StartsWith(deviceName, StringComparison.Ordinal));
    }

    private static string SanitizeScreenDeviceName(string name) =>
        name.Replace(@"\\.\", string.Empty);

    private static List<(Screen, DisplayDeviceData)>? GetDisplayScreens(DisplayDeviceData[] devices)
    {
        var result = new List<(Screen, DisplayDeviceData)>();

        foreach (var screen in Screen.AllScreens)
        {
            Log.Logger.Verbose("Screen: {DeviceName}", screen.DeviceName);

            var deviceData = GetDeviceMatchingScreen(devices, screen);
            if (deviceData == null)
            {
                return null;
            }

            Log.Logger.Verbose("Matching device: {DeviceString}, {DeviceId}", deviceData.DeviceString, deviceData.DeviceId);
            result.Add((screen, deviceData));
        }

        return result;
    }
}
