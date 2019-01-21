namespace OnlyM.Services
{
    using System.Windows;
    using System.Windows.Forms;
    using OnlyM.Core.Models;
    using OnlyM.Core.Services.CommandLine;
    using OnlyM.Core.Services.Options;
    using Serilog;

    internal static class MediaWindowPositionHelper
    {
        public static void PositionMediaWindow(
            IOptionsService optionsService,
            ICommandLineService commandLineService,
            Window window, 
            Screen monitor, 
            (int dpiX, int dpiY) systemDpi, 
            bool isVideo)
        {
            var area = monitor.WorkingArea;

            var left = (area.Left * 96) / systemDpi.dpiX;
            var top = (area.Top * 96) / systemDpi.dpiY;
            var width = (area.Width * 96) / systemDpi.dpiX;
            var height = (area.Height * 96) / systemDpi.dpiY;

            Log.Logger.Verbose($"Monitor = {monitor.DeviceName} Left = {left}, top = {top}");

            if (WpfMediaFoundationHackRequired(optionsService, commandLineService, monitor, isVideo))
            {
                PositionWindowUsingHack(window, monitor, left, top, width, height);
            }
            else
            {
                window.Left = left;
                window.Top = top;
                window.Width = width;
                window.Height = height;
            }
        }

        private static void PositionWindowUsingHack(
            Window window, Screen monitor, int left, int top, int width, int height)
        {
            var primaryMonitor = Screen.PrimaryScreen;

            if (MonitorToRightOf(monitor, primaryMonitor))
            {
                window.Left = left - 1;
                window.Top = top;
                window.Width = width + 1;
                window.Height = height;
            }
            else if (MonitorToLeftOf(monitor, primaryMonitor))
            {
                window.Left = left;
                window.Top = top;
                window.Width = width + 1;
                window.Height = height;
            }
            else if (MonitorIsAbove(monitor, primaryMonitor))
            {
                window.Left = left;
                window.Top = top;
                window.Width = width;
                window.Height = height + 1;
            }
            else if (MonitorIsBelow(monitor, primaryMonitor))
            {
                window.Left = left;
                window.Top = top - 1;
                window.Width = width;
                window.Height = height + 1;
            }
            else
            {
                window.Left = left;
                window.Top = top;
                window.Width = width;
                window.Height = height;
            }
        }

        private static bool WpfMediaFoundationHackRequired(
            IOptionsService optionsService,
            ICommandLineService commandLineService,
            Screen monitor, 
            bool isVideo)
        {
            // see http://stackoverflow.com/questions/4189660/why-does-wpf-mediaelement-not-work-on-secondary-monitor
            // see https://github.com/AntonyCorbett/OnlyM/issues/195
            var primaryMonitor = Screen.PrimaryScreen;

            return
                optionsService.RenderingMethod == RenderingMethod.MediaFoundation &&
                isVideo &&
                !commandLineService.NoGpu &&
                !commandLineService.DisableVideoRenderingFix &&
                !primaryMonitor.Equals(monitor);
        }

        private static bool MonitorToRightOf(Screen monitor1, Screen monitor2)
        {
            return monitor1.Bounds.Left >= monitor2.Bounds.Right;
        }

        private static bool MonitorToLeftOf(Screen monitor1, Screen monitor2)
        {
            return monitor1.Bounds.Right <= monitor2.Bounds.Left;
        }

        private static bool MonitorIsAbove(Screen monitor1, Screen monitor2)
        {
            return monitor1.Bounds.Bottom <= monitor2.Bounds.Top;
        }

        private static bool MonitorIsBelow(Screen monitor1, Screen monitor2)
        {
            return monitor1.Bounds.Top <= monitor2.Bounds.Bottom;
        }
    }
}
