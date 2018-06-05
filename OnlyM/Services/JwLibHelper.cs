namespace OnlyM.Services
{
    using System;
    using System.Diagnostics;
    using System.Linq;

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

            var desktopWindow = JwLibHelperNativeMethods.GetDesktopWindow();
            if (desktopWindow == IntPtr.Zero)
            {
                return;
            }
            
            var found = false;
            var prevWindow = IntPtr.Zero;

            while (!found)
            {
                var nextWindow = JwLibHelperNativeMethods.FindWindowEx(desktopWindow, prevWindow, null, null);
                if (nextWindow != IntPtr.Zero)
                {
                    JwLibHelperNativeMethods.GetWindowThreadProcessId(nextWindow, out var procId);
                    if (procId == p.Id)
                    {
                        found = true;
                        JwLibHelperNativeMethods.SetForegroundWindow(nextWindow);
                    }

                    prevWindow = nextWindow;
                }
                else
                {
                    break;
                }
            }
        }
    }
}
