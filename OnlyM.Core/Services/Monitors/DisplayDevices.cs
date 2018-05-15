namespace OnlyM.Core.Services.Monitors
{
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using Models;
    using Serilog;

    /// <summary>
    /// Queries the system for information regarding display devices
    /// </summary>
    internal static class DisplayDevices
    {
        /// <summary>
        /// Gets system display devices
        /// </summary>
        /// <returns>Collection of DisplayDeviceData</returns>
        public static IEnumerable<DisplayDeviceData> ReadDisplayDevices()
        {
            Log.Logger.Information("Reading display devices");
                
            var result = new List<DisplayDeviceData>();

            for (uint id = 0;; id++)
            {
                Log.Logger.Information($"Seeking device {id}");
                
                EnumDisplayNativeMethods.DISPLAY_DEVICE device1 = new EnumDisplayNativeMethods.DISPLAY_DEVICE();
                device1.cb = Marshal.SizeOf(device1);

                bool rv = EnumDisplayNativeMethods.EnumDisplayDevices(null, id, ref device1, 0);
                Log.Logger.Information($"EnumDisplayDevices retval = {rv}");

                if (!rv)
                {
                    break;
                }

                Log.Logger.Information($"Device name: {device1.DeviceName}");
                
                if (device1.StateFlags.HasFlag(EnumDisplayNativeMethods.DisplayDeviceStateFlags.AttachedToDesktop))
                {
                    Log.Logger.Information("Device attached to desktop");
                    
                    EnumDisplayNativeMethods.DISPLAY_DEVICE device2 = new EnumDisplayNativeMethods.DISPLAY_DEVICE();
                    device2.cb = Marshal.SizeOf(device2);

                    rv = EnumDisplayNativeMethods.EnumDisplayDevices(device1.DeviceName, 0, ref device2, 0);
                    Log.Logger.Information($"Secondary EnumDisplayDevices retval = {rv}");
                    
                    if (rv && device2.StateFlags.HasFlag(EnumDisplayNativeMethods.DisplayDeviceStateFlags.AttachedToDesktop))
                    {
                        Log.Logger.Information($"Display device data = {device2.DeviceName}, {device2.DeviceID}");
                        
                        result.Add(new DisplayDeviceData
                        {
                            Name = device2.DeviceName,
                            DeviceId = device2.DeviceID,
                            DeviceString = device2.DeviceString,
                            DeviceKey = device2.DeviceKey
                        });
                    }
                }
            }

            return result;
        }
    }
}
