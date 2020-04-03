namespace OnlyM.Services
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Forms;
    using System.Windows.Media;
    using OnlyM.Core.Models;
    using OnlyM.Core.Services.CommandLine;
    using OnlyM.Core.Services.Options;
    using OnlyM.Windows;
    using Serilog;

    /// <summary>
    /// Responsible for positioning the media window and working around the WPF MediaFoundation
    /// issue that affects rendering of H265 video.
    /// See http://stackoverflow.com/questions/4189660/why-does-wpf-mediaelement-not-work-on-secondary-monitor
    /// See https://github.com/AntonyCorbett/OnlyM/issues/195
    /// The fix is a bit of a hack - we force the media window to spill over onto the primary
    /// monitor (if possible) by 1 pixel and then adjust the media window main grid margin
    /// to ensure that the video isn't shown in that pixel. This leaves a black 1-pixel line
    /// at the edge of the primary display and is rarely noticed.
    /// We ensure that the hack is only implemented when absolutely necessary.
    /// </summary>
    internal static class MediaWindowPositionHelper
    {
        public static void PositionMediaWindow(
            IOptionsService optionsService,
            ICommandLineService commandLineService,
            Window mediaWindow, 
            Screen monitor, 
            (int dpiX, int dpiY) systemDpi, 
            bool isVideo)
        {
            var area = monitor.Bounds;

            var left = (area.Left * 96) / systemDpi.dpiX;
            var top = (area.Top * 96) / systemDpi.dpiY;
            var width = (area.Width * 96) / systemDpi.dpiX;
            var height = (area.Height * 96) / systemDpi.dpiY;

            Log.Logger.Verbose($"Monitor = {monitor.DeviceName} Left = {left}, top = {top}");

            PrepareForFullScreenMonitorDisplay(mediaWindow);

            var mainGrid = GetMainGrid(mediaWindow);
            Debug.Assert(mainGrid != null || !isVideo, "mainGrid != null");

            if (WpfMediaFoundationHackRequired(optionsService, commandLineService, monitor, isVideo))
            {
                PositionWindowUsingHack(mediaWindow, mainGrid, monitor, left, top, width, height);
            }
            else
            {
                mediaWindow.Left = left;
                mediaWindow.Top = top;
                mediaWindow.Width = width;
                mediaWindow.Height = height;

                if (mainGrid != null)
                {
                    mainGrid.Margin = new Thickness(0);
                }
            }
        }

        public static void PositionMediaWindowWindowed(MediaWindow mediaWindow)
        {
            mediaWindow.IsWindowed = true;
            PrepareForWindowedDisplay(mediaWindow);
            mediaWindow.RestoreWindowPositionAndSize();
        }

        private static void PrepareForFullScreenMonitorDisplay(Window mediaWindow)
        {
            mediaWindow.ResizeMode = ResizeMode.NoResize;
            mediaWindow.ShowInTaskbar = false;
            mediaWindow.WindowStyle = WindowStyle.None;
        }

        private static void PrepareForWindowedDisplay(Window mediaWindow)
        {
            mediaWindow.ResizeMode = ResizeMode.CanResize;
            mediaWindow.ShowInTaskbar = true;
            mediaWindow.WindowStyle = WindowStyle.SingleBorderWindow;
        }

        private static Grid GetMainGrid(Window mediaWindow)
        {
            return FindVisualChildren<Grid>(mediaWindow).FirstOrDefault();
        }

        private static void PositionWindowUsingHack(
            Window mediaWindow, Grid mainGrid, Screen monitor, int left, int top, int width, int height)
        {
            Log.Logger.Verbose("Positioning media window according to WPF Media Foundation hack");

            var primaryMonitor = Screen.PrimaryScreen;
            
            if (MonitorToRightOf(monitor, primaryMonitor))
            {
                mediaWindow.Left = left - 1;
                mediaWindow.Top = top;
                mediaWindow.Width = width + 1;
                mediaWindow.Height = height;

                if (mainGrid != null)
                {
                    mainGrid.Margin = new Thickness(1, 0, 0, 0);
                }
            }
            else if (MonitorToLeftOf(monitor, primaryMonitor))
            {
                mediaWindow.Left = left;
                mediaWindow.Top = top;
                mediaWindow.Width = width + 1;
                mediaWindow.Height = height;

                if (mainGrid != null)
                {
                    mainGrid.Margin = new Thickness(0, 0, 1, 0);
                }
            }
            else if (MonitorIsAbove(monitor, primaryMonitor))
            {
                mediaWindow.Left = left;
                mediaWindow.Top = top;
                mediaWindow.Width = width;
                mediaWindow.Height = height + 1;

                if (mainGrid != null)
                {
                    mainGrid.Margin = new Thickness(0, 0, 0, 1);
                }
            }
            else if (MonitorIsBelow(monitor, primaryMonitor))
            {
                mediaWindow.Left = left;
                mediaWindow.Top = top - 1;
                mediaWindow.Width = width;
                mediaWindow.Height = height + 1;

                if (mainGrid != null)
                {
                    mainGrid.Margin = new Thickness(0, 1, 0, 0);
                }
            }
            else
            {
                // media monitor does not abutt the primary monitor
                // so the hack is abandoned.
                mediaWindow.Left = left;
                mediaWindow.Top = top;
                mediaWindow.Width = width;
                mediaWindow.Height = height;

                if (mainGrid != null)
                {
                    mainGrid.Margin = new Thickness(0);
                }
            }
        }

        private static bool WpfMediaFoundationHackRequired(
            IOptionsService optionsService,
            ICommandLineService commandLineService,
            Screen monitor, 
            bool isVideo)
        {
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

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) 
            where T : DependencyObject
        {
            if (depObj != null)
            {
                for (var i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    var child = VisualTreeHelper.GetChild(depObj, i);
                    if (child is T children)
                    {
                        yield return children;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }
    }
}
