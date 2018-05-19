namespace OnlyM.Services
{
    // ReSharper disable StyleCop.SA1305
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.InteropServices;

    internal static class JwLibHelper
    {
        private const string JwLibProcessName = "JWLibrary";

        public static void BringToFront()
        {
            var p = Process.GetProcessesByName(JwLibProcessName).FirstOrDefault();
            if (p == null)
            {
                return;
            }

            var desktopWindow = GetDesktopWindow();
            if (desktopWindow == IntPtr.Zero)
            {
                return;
            }
            
            var found = false;
            var prevWindow = IntPtr.Zero;

            while (!found)
            {
                var nextWindow = FindWindowEx(desktopWindow, prevWindow, null, null);
                if (nextWindow != IntPtr.Zero)
                {
                    GetWindowThreadProcessId(nextWindow, out var procId);
                    if (procId == p.Id)
                    {
                        found = true;
                        SetForegroundWindow(nextWindow);
                    }

                    prevWindow = nextWindow;
                }
                else
                {
                    break;
                }
            }
        }

        [DllImport("user32.dll", SetLastError = false)]
        private static extern IntPtr GetDesktopWindow();
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(
            IntPtr hwndParent, 
            IntPtr hwndChildAfter, 
            string lpszClass,
            string lpszWindow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        
        [DllImport("User32.dll")]
        private static extern bool SetForegroundWindow(IntPtr handle);
    }
}
