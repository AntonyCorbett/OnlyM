using System;
using System.Runtime.InteropServices;

namespace OnlyM.Core.Services.Monitors
{
    // ReSharper disable StyleCop.SA1602
    // ReSharper disable UnusedMember.Global
    // ReSharper disable InconsistentNaming
    // ReSharper disable MemberCanBePrivate.Global
    // ReSharper disable FieldCanBeMadeReadOnly.Global

#pragma warning disable S101 // Types should be named in PascalCase

    /// <summary>
    /// Native methods associated with retrieval of display device data
    /// </summary>
    internal static class EnumDisplayNativeMethods
    {
        [Flags]
#pragma warning disable S2344 // Enumeration type names should not have "Flags" or "Enum" suffixes
        public enum DisplayDeviceStateFlags
#pragma warning restore S2344 // Enumeration type names should not have "Flags" or "Enum" suffixes
        {
            /// <summary>The device is part of the desktop.</summary>
            AttachedToDesktop = 0x1,

            MultiDriver = 0x2,
            
            /// <summary>The device is part of the desktop.</summary>
            PrimaryDevice = 0x4,
            
            /// <summary>Represents a pseudo device used to mirror application drawing for remoting or other purposes.</summary>
            MirroringDriver = 0x8,
            
            /// <summary>The device is VGA compatible.</summary>
            VGACompatible = 0x10,
            
            /// <summary>The device is removable; it cannot be the primary display.</summary>
            Removable = 0x20,
            
            /// <summary>The device has more display modes than its output devices support.</summary>
            ModesPruned = 0x8000000,

            Remote = 0x4000000,

            Disconnect = 0x2000000,
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool EnumDisplayDevices(
            string? lpDevice,
            uint iDevNum,
            ref DISPLAY_DEVICE lpDisplayDevice,
            uint dwFlags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DISPLAY_DEVICE
        {
            [MarshalAs(UnmanagedType.U4)]
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            [MarshalAs(UnmanagedType.U4)]
            public DisplayDeviceStateFlags StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }
    }

#pragma warning restore S101 // Types should be named in PascalCase
}
