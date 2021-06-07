namespace OnlyM.CoreSys.WindowsPositioning
{
    // ReSharper disable CommentTypo
    // ReSharper disable IdentifierTypo
    // ReSharper disable InconsistentNaming
    // ReSharper disable StyleCop.SA1307
    // ReSharper disable MemberCanBePrivate.Global
    // ReSharper disable FieldCanBeMadeReadOnly.Global
    // ReSharper disable StyleCop.SA1203
    // ReSharper disable StyleCop.SA1310
    // ReSharper disable UnusedMember.Global

#pragma warning disable S101 // Types should be named in PascalCase
#pragma warning disable U2U1004 // Public value types should implement equality

    // adapted from david Rickard's Tech Blog
    using System;
    using System.IO;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Windows;
    using System.Windows.Interop;
    using System.Xml;
    using System.Xml.Serialization;
    using Serilog;

    public static class WindowsPlacement
    {
        private const int SwShowNormal = 1;
        private const int SwShowMinimized = 2;

        private static readonly Encoding Encoding = new UTF8Encoding();
        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(WINDOWPLACEMENT));

        public static void SetPlacement(this Window window, string placementJson)
        {
            var windowHandle = new WindowInteropHelper(window).Handle;

            if (!string.IsNullOrEmpty(placementJson))
            {
                var xmlBytes = Encoding.GetBytes(placementJson);
                try
                {
                    WINDOWPLACEMENT placement;
                    using (var memoryStream = new MemoryStream(xmlBytes))
                    {
                        var obj = (WINDOWPLACEMENT?)Serializer.Deserialize(memoryStream);
                        if (obj == null)
                        {
                            return;
                        }

                        placement = obj.Value;
                    }

                    placement.length = Marshal.SizeOf(typeof(WINDOWPLACEMENT));
                    placement.flags = 0;
                    placement.showCmd = placement.showCmd == SwShowMinimized ? SwShowNormal : placement.showCmd;
                    WindowsPlacementNativeMethods.SetWindowPlacement(windowHandle, ref placement);
                }
                catch (InvalidOperationException ex)
                {
                    Log.Logger.Error(ex, "Parsing placement XML failed");
                }
            }
        }

        public static string GetPlacement(this Window window)
        {
            return GetPlacement(new WindowInteropHelper(window).Handle);
        }

        public static (int x, int y) GetDpiSettings()
        {
#pragma warning disable S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
            var dpiXProperty = typeof(SystemParameters).GetProperty("DpiX", BindingFlags.NonPublic | BindingFlags.Static);
            var dpiYProperty = typeof(SystemParameters).GetProperty("Dpi", BindingFlags.NonPublic | BindingFlags.Static);
#pragma warning restore S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields

            if (dpiXProperty == null || dpiYProperty == null)
            {
                return (96, 96);
            }

            return ((int)(dpiXProperty.GetValue(null, null) ?? 96), (int)(dpiYProperty.GetValue(null, null) ?? 96));
        }

        private static string GetPlacement(IntPtr windowHandle)
        {
            WindowsPlacementNativeMethods.GetWindowPlacement(windowHandle, out var placement);

            using (var memoryStream = new MemoryStream())
            {
                var xmlTextWriter = new XmlTextWriter(memoryStream, Encoding.UTF8);
                Serializer.Serialize(xmlTextWriter, placement);
                var xmlBytes = memoryStream.ToArray();
                return Encoding.GetString(xmlBytes);
            }
        }
    }

    // RECT structure required by WINDOWPLACEMENT structure
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public RECT(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }
    }

    // POINT structure required by WINDOWPLACEMENT structure
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;

        public POINT(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    // WINDOWPLACEMENT stores the position, size, and state of a window
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public POINT minPosition;
        public POINT maxPosition;
        public RECT normalPosition;
    }

#pragma warning restore U2U1004 // Public value types should implement equality
#pragma warning restore S101 // Types should be named in PascalCase
}
