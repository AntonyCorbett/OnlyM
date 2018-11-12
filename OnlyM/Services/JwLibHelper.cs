namespace OnlyM.Services
{
    using System;
    using System.Diagnostics;
    using System.Linq;

    internal static class JwLibHelper
    {
        private const string JwLibProcessName = "JWLibrary";
        private const string JwLibSignLanguageProcessName = "JWLibrary.Forms.UWP";

        public static void BringToFront()
        {
            BringToFront(JwLibProcessName);
            BringToFront(JwLibSignLanguageProcessName);
        }

        private static void BringToFront(string processName)
        {
            var p = Process.GetProcessesByName(processName).FirstOrDefault();
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

                        IntPtr mainWindow = p.MainWindowHandle;
                        if (JwLibHelperNativeMethods.IsIconic(mainWindow))
                        {
                            const int swRestore = 9;
                            JwLibHelperNativeMethods.ShowWindow(mainWindow, swRestore);
                        }

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
