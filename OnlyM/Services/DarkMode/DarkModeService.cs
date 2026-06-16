using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Messaging;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using OnlyM.Core.Models;
using OnlyM.Core.Services.Options;
using OnlyM.PubSubMessages;

namespace OnlyM.Services.DarkMode;

internal sealed class DarkModeService : IDarkModeService
{
    // DWMWA_USE_IMMERSIVE_DARK_MODE (attribute 20) is available from Windows 10 20H1 onward.
    private const int DwmwaUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll")]
#pragma warning disable SYSLIB1054
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);
#pragma warning restore SYSLIB1054

    private readonly IOptionsService _optionsService;

    // Captured once from the palette resource dictionaries before we ever touch them.
    private SolidColorBrush? _originalLightBrush;
    private SolidColorBrush? _originalMidBrush;
    private SolidColorBrush? _originalDarkBrush;

    public DarkModeService(IOptionsService optionsService)
    {
        _optionsService = optionsService;
        CacheOriginalPrimaryBrushes();
        _optionsService.DarkModeChangedEvent += OnDarkModeChangedEvent;
        ApplyTheme();
    }

    public void SystemThemeChanged() => ApplyTheme();

    public void ApplyTitleBarTheme(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle != IntPtr.Zero)
        {
            SetTitleBarDarkMode(handle, ThemeState.IsDark);
        }
    }

    private void CacheOriginalPrimaryBrushes()
    {
        _originalLightBrush = Application.Current.Resources["PrimaryHueLightBrush"] as SolidColorBrush;
        _originalMidBrush = Application.Current.Resources["PrimaryHueMidBrush"] as SolidColorBrush;
        _originalDarkBrush = Application.Current.Resources["PrimaryHueDarkBrush"] as SolidColorBrush;
    }

    private void OnDarkModeChangedEvent(object? sender, EventArgs e) => ApplyTheme();

    private void ApplyTheme()
    {
        var isDark = _optionsService.DarkModeOption switch
        {
            DarkModeOption.Dark => true,
            DarkModeOption.Light => false,
            _ => IsSystemDarkMode(),
        };

        Application.Current.Dispatcher.Invoke(() =>
        {
            var paletteHelper = new PaletteHelper();
            var theme = paletteHelper.GetTheme();
            theme.SetBaseTheme(isDark ? Theme.Dark : Theme.Light);
            paletteHelper.SetTheme(theme);

            // PaletteHelper.GetTheme() reads whatever is currently in resources, so if the
            // previous call was dark mode the primary hues will be the lightened variants.
            // SetTheme() then writes those values back unchanged. We must therefore restore
            // the correct hues explicitly after every SetTheme() call.
            if (isDark)
            {
                // Shift to lighter DeepPurple variants so primary-coloured text and accents
                // (e.g. floating ComboBox hints) are readable on the near-black background.
                Application.Current.Resources["PrimaryHueLightBrush"] = new SolidColorBrush(Color.FromRgb(0xD1, 0xC4, 0xE9)); // DeepPurple 100
                Application.Current.Resources["PrimaryHueMidBrush"] = new SolidColorBrush(Color.FromRgb(0xB3, 0x9D, 0xDB)); // DeepPurple 200
                Application.Current.Resources["PrimaryHueDarkBrush"] = new SolidColorBrush(Color.FromRgb(0x95, 0x75, 0xCD)); // DeepPurple 300
            }
            else if (_originalLightBrush != null)
            {
                // Restore the exact palette values captured at startup so light mode is
                // pixel-identical to what it was before any dark mode was ever applied.
                Application.Current.Resources["PrimaryHueLightBrush"] = _originalLightBrush;
                Application.Current.Resources["PrimaryHueMidBrush"] = _originalMidBrush;
                Application.Current.Resources["PrimaryHueDarkBrush"] = _originalDarkBrush;
            }

            // Override the nav-bar background, which is hardcoded in MainWindow.xaml.
            Application.Current.Resources["OnlyMNavBarBackground"] = new SolidColorBrush(
                isDark
                    ? Color.FromRgb(0x37, 0x30, 0x4A) // dark desaturated purple
                    : Color.FromRgb(0xB3, 0x9D, 0xDB)); // DeepPurple 200

            // Apply dark/light title bar to every open window via the DWM API.
            foreach (Window window in Application.Current.Windows)
            {
                var handle = new WindowInteropHelper(window).Handle;
                if (handle != IntPtr.Zero)
                {
                    SetTitleBarDarkMode(handle, isDark);
                }
            }

            // Set shared state before broadcasting so any newly constructed consumer
            // reads the correct value immediately without waiting for the message.
            ThemeState.IsDark = isDark;
            WeakReferenceMessenger.Default.Send(new ThemeChangedMessage { IsDark = isDark });
        });
    }

    private static void SetTitleBarDarkMode(IntPtr hwnd, bool isDark)
    {
        var value = isDark ? 1 : 0;

        // Discard the HRESULT — failure just means the title bar stays its default colour.
        _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref value, Marshal.SizeOf(value));
    }

    private static bool IsSystemDarkMode()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int appsUseLightTheme && appsUseLightTheme == 0;
    }
}
