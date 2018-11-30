namespace OnlyM.Services
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;

    internal static class JwLibHelper
    {
        private const string JwLibProcessName = "JWLibrary";
        private const string JwLibSignLanguageProcessName = "JWLibrary.Forms.UWP";
        private const string MainWindowClassName = "ApplicationFrameWindow";
        private const string JwLibCaptionPrefix = "JW Library";

        public static void BringToFront()
        {
            if (!BringToFront(JwLibProcessName))
            {
                BringToFront(JwLibSignLanguageProcessName);
            }
        }

        private static bool BringToFront(string processName)
        {
            var p = Process.GetProcessesByName(processName).FirstOrDefault();
            if (p == null)
            {
                return false;
            }

            var desktopWindow = JwLibHelperNativeMethods.GetDesktopWindow();
            if (desktopWindow == IntPtr.Zero)
            {
                return false;
            }

            bool found = false;
            var prevWindow = IntPtr.Zero;

            while (!found)
            {
                var mainWindow = JwLibHelperNativeMethods.FindWindowEx(desktopWindow, prevWindow, MainWindowClassName, null);
                if (mainWindow == IntPtr.Zero)
                {
                    break;
                }

                var sb = new StringBuilder(256);
                JwLibHelperNativeMethods.GetWindowText(mainWindow, sb, 256);
                if (sb.ToString().StartsWith(JwLibCaptionPrefix))
                {
                    JwLibHelperNativeMethods.SetForegroundWindow(mainWindow);
                    found = true;
                }

                prevWindow = mainWindow;
            }

            return found;
        }
    }
}
