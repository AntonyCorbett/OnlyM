using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Serilog;

// ReSharper disable UnusedMember.Global
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming

#pragma warning disable U2U1004

namespace OnlyM.Core.Services.Monitors;

//// see https://stackoverflow.com/a/28257839/8576725

public static partial class ScreenQuery
{
#pragma warning disable SA1307 // Accessible fields must begin with upper-case letter
#pragma warning disable SA1202 // Elements must be ordered by access
#pragma warning disable SA1313 // Parameter names must begin with lower-case letter
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CA1707 // Identifiers should not contain underscores
#pragma warning disable CA1712 // Do not prefix enum values with type name
    private const int ErrorSuccess = 0;
    private const int ErrorInvalidParameter = 87;
    private const int ErrorInsufficientBuffer = 122;

    public enum QUERY_DEVICE_CONFIG_FLAGS : uint
    {
        QDC_ALL_PATHS = 0x00000001,
        QDC_ONLY_ACTIVE_PATHS = 0x00000002,
        QDC_DATABASE_CURRENT = 0x00000004,
    }

    public enum DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY : uint
    {
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_OTHER = 0xFFFFFFFF,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_HD15 = 0,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_SVIDEO = 1,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_COMPOSITE_VIDEO = 2,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_COMPONENT_VIDEO = 3,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_DVI = 4,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_HDMI = 5,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_LVDS = 6,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_D_JPN = 8,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_SDI = 9,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_DISPLAYPORT_EXTERNAL = 10,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_DISPLAYPORT_EMBEDDED = 11,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_UDI_EXTERNAL = 12,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_UDI_EMBEDDED = 13,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_SDTVDONGLE = 14,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_MIRACAST = 15,
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_INTERNAL = 0x80000000,
#pragma warning disable CA1069 // Enums values should not be duplicated
        DISPLAYCONFIG_OUTPUT_TECHNOLOGY_FORCE_UINT32 = 0xFFFFFFFF,
#pragma warning restore CA1069 // Enums values should not be duplicated
    }

    public enum DISPLAYCONFIG_SCANLINE_ORDERING : uint
    {
        DISPLAYCONFIG_SCANLINE_ORDERING_UNSPECIFIED = 0,
        DISPLAYCONFIG_SCANLINE_ORDERING_PROGRESSIVE = 1,
        DISPLAYCONFIG_SCANLINE_ORDERING_INTERLACED = 2,
        DISPLAYCONFIG_SCANLINE_ORDERING_INTERLACED_UPPERFIELDFIRST = DISPLAYCONFIG_SCANLINE_ORDERING_INTERLACED,
        DISPLAYCONFIG_SCANLINE_ORDERING_INTERLACED_LOWERFIELDFIRST = 3,
        DISPLAYCONFIG_SCANLINE_ORDERING_FORCE_UINT32 = 0xFFFFFFFF,
    }

    public enum DISPLAYCONFIG_ROTATION : uint
    {
        DISPLAYCONFIG_ROTATION_IDENTITY = 1,
        DISPLAYCONFIG_ROTATION_ROTATE90 = 2,
        DISPLAYCONFIG_ROTATION_ROTATE180 = 3,
        DISPLAYCONFIG_ROTATION_ROTATE270 = 4,
        DISPLAYCONFIG_ROTATION_FORCE_UINT32 = 0xFFFFFFFF,
    }

    public enum DISPLAYCONFIG_SCALING : uint
    {
        DISPLAYCONFIG_SCALING_IDENTITY = 1,
        DISPLAYCONFIG_SCALING_CENTERED = 2,
        DISPLAYCONFIG_SCALING_STRETCHED = 3,
        DISPLAYCONFIG_SCALING_ASPECTRATIOCENTEREDMAX = 4,
        DISPLAYCONFIG_SCALING_CUSTOM = 5,
        DISPLAYCONFIG_SCALING_PREFERRED = 128,
        DISPLAYCONFIG_SCALING_FORCE_UINT32 = 0xFFFFFFFF,
    }

    public enum DISPLAYCONFIG_PIXELFORMAT : uint
    {
        DISPLAYCONFIG_PIXELFORMAT_8BPP = 1,
        DISPLAYCONFIG_PIXELFORMAT_16BPP = 2,
        DISPLAYCONFIG_PIXELFORMAT_24BPP = 3,
        DISPLAYCONFIG_PIXELFORMAT_32BPP = 4,
        DISPLAYCONFIG_PIXELFORMAT_NONGDI = 5,
        DISPLAYCONFIG_PIXELFORMAT_FORCE_UINT32 = 0xffffffff,
    }

    public enum DISPLAYCONFIG_MODE_INFO_TYPE : uint
    {
        DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE = 1,
        DISPLAYCONFIG_MODE_INFO_TYPE_TARGET = 2,
        DISPLAYCONFIG_MODE_INFO_TYPE_FORCE_UINT32 = 0xFFFFFFFF,
    }

    public enum DISPLAYCONFIG_DEVICE_INFO_TYPE : uint
    {
        DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1,
        DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2,
        DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_PREFERRED_MODE = 3,
        DISPLAYCONFIG_DEVICE_INFO_GET_ADAPTER_NAME = 4,
        DISPLAYCONFIG_DEVICE_INFO_SET_TARGET_PERSISTENCE = 5,
        DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_BASE_TYPE = 6,
        DISPLAYCONFIG_DEVICE_INFO_FORCE_UINT32 = 0xFFFFFFFF,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_PATH_SOURCE_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_PATH_TARGET_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        private DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY outputTechnology;
        private DISPLAYCONFIG_ROTATION rotation;
        private DISPLAYCONFIG_SCALING scaling;
        private DISPLAYCONFIG_RATIONAL refreshRate;
        private DISPLAYCONFIG_SCANLINE_ORDERING scanLineOrdering;

        // IMPORTANT: BOOL in Win32 is 4 bytes. Was 'bool' previously (incorrect layout).
        private int targetAvailable; // marshals Win32 BOOL
        public uint statusFlags;

        public bool TargetAvailable => targetAvailable != 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_RATIONAL
    {
        public uint Numerator;
        public uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_PATH_INFO
    {
        public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
        public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_2DREGION
    {
        public uint cx;
        public uint cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_VIDEO_SIGNAL_INFO
    {
        public ulong pixelRate;
        public DISPLAYCONFIG_RATIONAL hSyncFreq;
        public DISPLAYCONFIG_RATIONAL vSyncFreq;
        public DISPLAYCONFIG_2DREGION activeSize;
        public DISPLAYCONFIG_2DREGION totalSize;
        public uint videoStandard;
        public DISPLAYCONFIG_SCANLINE_ORDERING scanLineOrdering;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_TARGET_MODE
    {
        public DISPLAYCONFIG_VIDEO_SIGNAL_INFO targetVideoSignalInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINTL
    {
        private int x;
        private int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_SOURCE_MODE
    {
        public uint width;
        public uint height;
        public DISPLAYCONFIG_PIXELFORMAT pixelFormat;
        public POINTL position;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct DISPLAYCONFIG_MODE_INFO_UNION
    {
        [FieldOffset(0)]
        public DISPLAYCONFIG_TARGET_MODE targetMode;

        [FieldOffset(0)]
        public DISPLAYCONFIG_SOURCE_MODE sourceMode;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_MODE_INFO
    {
        public DISPLAYCONFIG_MODE_INFO_TYPE infoType;
        public uint id;
        public LUID adapterId;
        public DISPLAYCONFIG_MODE_INFO_UNION modeInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_TARGET_DEVICE_NAME_FLAGS
    {
        public uint value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_DEVICE_INFO_HEADER
    {
        public DISPLAYCONFIG_DEVICE_INFO_TYPE type;
        public uint size;
        public LUID adapterId;
        public uint id;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DISPLAYCONFIG_TARGET_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public DISPLAYCONFIG_TARGET_DEVICE_NAME_FLAGS flags;
        public DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY outputTechnology;
        public ushort edidManufactureId;
        public ushort edidProductCodeId;
        public uint connectorInstance;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string monitorFriendlyDeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string monitorDevicePath;
    }

    [LibraryImport("user32.dll", EntryPoint = "GetDisplayConfigBufferSizes", SetLastError = false, StringMarshalling = StringMarshalling.Utf16)]
    private static partial int GetDisplayConfigBufferSizes(
        QUERY_DEVICE_CONFIG_FLAGS flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

#pragma warning disable SYSLIB1054 // not possible to use LibraryImport since DISPLAYCONFIG_PATH_INFO and DISPLAYCONFIG_MODE_INFO are not blittable
    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(
        QUERY_DEVICE_CONFIG_FLAGS flags,
        ref uint numPathArrayElements,
        [Out] DISPLAYCONFIG_PATH_INFO[] PathInfoArray,
        ref uint numModeInfoArrayElements,
        [Out] DISPLAYCONFIG_MODE_INFO[] ModeInfoArray,
        IntPtr currentTopologyId);
#pragma warning restore SYSLIB1054

#pragma warning disable SYSLIB1054 // not possible to use LibraryImport since DISPLAYCONFIG_TARGET_DEVICE_NAME is not blittable
    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_DEVICE_NAME deviceName);
#pragma warning restore SYSLIB1054

    private static string MonitorFriendlyName(LUID adapterId, uint targetId)
    {
        var deviceName = new DISPLAYCONFIG_TARGET_DEVICE_NAME
        {
            header =
            {
                size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>(),
                adapterId = adapterId,
                id = targetId,
                type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME,
            },
        };

        var error = DisplayConfigGetDeviceInfo(ref deviceName);
        if (error != ErrorSuccess)
        {
            throw new Win32Exception(error);
        }

        return deviceName.monitorFriendlyDeviceName;
    }

    // Attempts to acquire current display configuration with limited retries to
    // tolerate topology changes between buffer size and query calls.
    private static bool TryAcquireDisplayConfig(out DISPLAYCONFIG_MODE_INFO[] modes)
    {
        modes = [];

        const int maxAttempts = 5;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var err = GetDisplayConfigBufferSizes(QUERY_DEVICE_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS, out var pathCount, out var modeCount);
            if (err != ErrorSuccess)
            {
                if (err == ErrorInvalidParameter || err == ErrorInsufficientBuffer)
                {
                    continue;
                }

                throw new Win32Exception(err);
            }

            if (pathCount == 0 && modeCount == 0)
            {
                return true;
            }

            var localPaths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var localModes = new DISPLAYCONFIG_MODE_INFO[modeCount];

            var pc = pathCount;
            var mc = modeCount;
            err = QueryDisplayConfig(
                QUERY_DEVICE_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS,
                ref pc,
                localPaths,
                ref mc,
                localModes,
                IntPtr.Zero);

            if (err == ErrorSuccess)
            {
                // Trim if API returned fewer than allocated.
                if (pc != pathCount)
                {
                    Array.Resize(ref localPaths, (int)pc);
                }

                if (mc != modeCount)
                {
                    Array.Resize(ref localModes, (int)mc);
                }

                modes = localModes;
                return true;
            }

            if (err == ErrorInsufficientBuffer || err == ErrorInvalidParameter)
            {
                // Topology likely changed in-between; retry.
                continue;
            }

            // Unexpected error.
            throw new Win32Exception(err);
        }

        return false;
    }

    private static IEnumerable<string> GetAllMonitorsFriendlyNames()
    {
        DISPLAYCONFIG_MODE_INFO[] displayModes;

        try
        {
            if (!TryAcquireDisplayConfig(out displayModes))
            {
                yield break;
            }
        }
        catch (Exception ex)
        {
            Log.Logger.Debug(ex, "Failed to acquire display configuration");
            yield break;
        }

        foreach (var displayMode in displayModes)
        {
            if (displayMode.infoType == DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_TARGET)
            {
                string? name = null;
                try
                {
                    name = MonitorFriendlyName(displayMode.adapterId, displayMode.id);
                }
                catch (Exception ex)
                {
                    Log.Logger.Debug(ex, "Failed to get friendly name for display target {Id}", displayMode.id);
                }

                if (!string.IsNullOrWhiteSpace(name))
                {
                    yield return name;
                }
            }
        }
    }

    public static string? DeviceFriendlyName(this Screen screen)
    {
        try
        {
            var allFriendlyNames = GetAllMonitorsFriendlyNames().ToArray();
            for (var index = 0; index < Screen.AllScreens.Length; index++)
            {
                if (Equals(screen, Screen.AllScreens[index]))
                {
                    if (index < allFriendlyNames.Length)
                    {
                        return allFriendlyNames[index];
                    }

                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Logger.Warning(ex, "Could not get monitor friendly names");
        }

        return null;
    }

#pragma warning restore CA1712 // Do not prefix enum values with type name
#pragma warning restore CA1707 // Identifiers should not contain underscores
#pragma warning restore IDE0044 // Add readonly modifier
#pragma warning restore SA1313 // Parameter names must begin with lower-case letter
#pragma warning restore SA1202 // Elements must be ordered by access
#pragma warning restore SA1307 // Accessible fields must begin with upper-case letter
}
