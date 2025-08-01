﻿using System.Collections.Generic;
using System.Runtime.InteropServices;
using OnlyM.Core.Models;
using Serilog;

namespace OnlyM.Core.Services.Monitors;

/// <summary>
/// Queries the system for information regarding display devices
/// </summary>
internal static class DisplayDevices
{
    /// <summary>
    /// Gets system display devices
    /// </summary>
    /// <returns>Collection of DisplayDeviceData</returns>
    public static List<DisplayDeviceData> ReadDisplayDevices()
    {
        Log.Logger.Debug("Reading display devices");

        var result = new List<DisplayDeviceData>();

        for (uint id = 0; ; id++)
        {
            Log.Logger.Verbose("Seeking device {DeviceId}", id);

            var device1 = default(EnumDisplayNativeMethods.DISPLAY_DEVICE);
            device1.cb = Marshal.SizeOf(device1);

            var rv = EnumDisplayNativeMethods.EnumDisplayDevices(null, id, ref device1, 0);
            Log.Logger.Verbose("EnumDisplayDevices retval = {rv}", rv);

            if (!rv)
            {
                break;
            }

            Log.Logger.Verbose("Device name: {DeviceName}", device1.DeviceName);

            if (device1.StateFlags.HasFlag(EnumDisplayNativeMethods.DisplayDeviceStateFlags.AttachedToDesktop))
            {
                Log.Logger.Verbose("Device attached to desktop");

                var device2 = default(EnumDisplayNativeMethods.DISPLAY_DEVICE);
                device2.cb = Marshal.SizeOf(device2);

                rv = EnumDisplayNativeMethods.EnumDisplayDevices(device1.DeviceName, 0, ref device2, 0);
                Log.Logger.Verbose("Secondary EnumDisplayDevices retval = {rv}", rv);

                if (rv && device2.StateFlags.HasFlag(EnumDisplayNativeMethods.DisplayDeviceStateFlags.AttachedToDesktop))
                {
                    Log.Logger.Verbose("Display device data = {DeviceName}, {DeviceID}", device2.DeviceName, device2.DeviceID);

                    result.Add(new DisplayDeviceData
                    {
                        Name = device2.DeviceName,
                        DeviceId = device2.DeviceID,
                        DeviceString = device2.DeviceString,
                        DeviceKey = device2.DeviceKey,
                    });
                }
            }
        }

        return result;
    }
}
