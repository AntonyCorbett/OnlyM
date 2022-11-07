using System;
using System.Runtime.InteropServices;
using System.Text;

namespace OnlyM.Services
{
    internal static class JwLibHelperNativeMethods
    {
        [DllImport("user32.dll", SetLastError = false)]
        public static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindowEx(
            IntPtr hwndParent,
            IntPtr hwndChildAfter,
            string lpszClass,
            string? lpszWindow);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("User32.dll")]
        public static extern bool SetForegroundWindow(IntPtr handle);

        [DllImport("User32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("User32.dll")]
        public static extern bool ShowWindow(IntPtr handle, int nCmdShow);

        // not concerned about performance of these calls so ignore CA2101
#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments
        [DllImport("User32.dll")]
#pragma warning disable CA1838 // Avoid 'StringBuilder' parameters for P/Invokes
        public static extern void GetClassName(IntPtr handle, StringBuilder s, int nMaxCount);
#pragma warning restore CA1838 // Avoid 'StringBuilder' parameters for P/Invokes
#pragma warning restore CA2101 // Specify marshaling for P/Invoke string arguments

        // not concerned about performance of these calls so ignore CA2101
#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments
        [DllImport("User32.dll")]
#pragma warning disable CA1838 // Avoid 'StringBuilder' parameters for P/Invokes
        public static extern void GetWindowText(IntPtr handle, StringBuilder s, int nMaxCount);
#pragma warning restore CA1838 // Avoid 'StringBuilder' parameters for P/Invokes
#pragma warning restore CA2101 // Specify marshaling for P/Invoke string arguments
    }
}
