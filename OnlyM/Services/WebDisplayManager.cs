﻿using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using CefSharp;
using CefSharp.Wpf;
using OnlyM.Core.Models;
using OnlyM.Core.Services.Database;
using OnlyM.Core.Services.Monitors;
using OnlyM.Core.Services.Options;
using OnlyM.Core.Services.WebShortcuts;
using OnlyM.CoreSys.Services.Snackbar;
using OnlyM.EventTracking;
using OnlyM.Models;
using OnlyM.Services.WebBrowser;
using Serilog;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace OnlyM.Services;

// Note that the Chromium browser instance is created in the Media Window XAML, and is a singleton.
// This means the browser page history is retained between several displays of a web page. It might
// be better to recreate the browser instance, thus clearing the history each time, but this seems 
// inefficient for such a minor requirement.

internal sealed class WebDisplayManager : IDisposable
{
    private readonly ChromiumWebBrowser _browser;
    private readonly FrameworkElement _browserGrid;
    private readonly IDatabaseService _databaseService;
    private readonly IOptionsService _optionsService;
    private readonly IMonitorsService _monitorsService;
    private readonly ISnackbarService _snackbarService;
    private Guid _mediaItemId;
    private bool _showing;
    private bool _useMirror;
    private string? _currentMediaItemUrl;
    private Process? _mirrorProcess;

    public WebDisplayManager(
        ChromiumWebBrowser browser,
        FrameworkElement browserGrid,
        IDatabaseService databaseService,
        IOptionsService optionsService,
        IMonitorsService monitorsService,
        ISnackbarService snackbarService)
    {
        _browser = browser;
        _browserGrid = browserGrid;
        _databaseService = databaseService;
        _optionsService = optionsService;
        _monitorsService = monitorsService;
        _snackbarService = snackbarService;

        InitBrowser();
    }

    public void Dispose()
    {
        _browser.Dispose();
        _mirrorProcess?.Dispose();
    }

    public event EventHandler<MediaEventArgs>? MediaChangeEvent;

    public event EventHandler<WebBrowserProgressEventArgs>? StatusEvent;

    public void NavigateToHomeUrl()
    {
        _browser.Load(_currentMediaItemUrl);
    }

    public void ShowWeb(
        string mediaItemFilePath,
        Guid mediaItemId,
        int pdfStartingPage,
        PdfViewStyle pdfViewStyle,
        bool showMirror,
        ScreenPosition screenPosition)
    {
        ArgumentNullException.ThrowIfNull(mediaItemFilePath);
        ArgumentNullException.ThrowIfNull(screenPosition);
        ArgumentOutOfRangeException.ThrowIfNegative(pdfStartingPage);

        _useMirror = showMirror;

        if (string.IsNullOrEmpty(mediaItemFilePath))
        {
            return;
        }

        _showing = false;
        _mediaItemId = mediaItemId;

        ScreenPositionHelper.SetScreenPosition(_browserGrid, screenPosition);

        OnMediaChangeEvent(CreateMediaEventArgs(mediaItemId, MediaChange.Starting));

        string? webAddress;
        if (Path.GetExtension(mediaItemFilePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            // https://www.adobe.com/content/dam/acom/en/devnet/acrobat/pdfs/pdf_open_parameters.pdf

            var viewString = GetPdfViewString(pdfViewStyle);
            var cacheBusterString = DateTime.Now.Ticks.ToString(CultureInfo.InvariantCulture);

            webAddress = $"pdf://{mediaItemFilePath}?t={cacheBusterString}#view={viewString}&page={pdfStartingPage}";
        }
        else
        {
            var urlHelper = new WebShortcutHelper(mediaItemFilePath);
            webAddress = urlHelper.Uri;
        }

        _currentMediaItemUrl = webAddress;

        RemoveAnimation();

        _browserGrid.Visibility = Visibility.Visible;

        _browser.Load(webAddress);
    }

    public void HideWeb()
    {
        OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Stopping));

        UpdateBrowserDataInDatabase();

        RemoveAnimation();

        FadeBrowser(false, () =>
        {
            OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Stopped));
            _browserGrid.Visibility = Visibility.Hidden;
        });
    }

    public async Task ShowMirrorAsync()
    {
        Log.Logger.Debug("Attempting to open mirror");

        if (Mutex.TryOpenExisting("OnlyMMirrorMutex", out var _))
        {
            Log.Logger.Debug("OnlyMMirrorMutex mutex exists");
            return;
        }

        var mirrorExePath = GetMirrorExePath();
        if (mirrorExePath == null)
        {
            return;
        }

        Log.Logger.Debug("Mirror path = {Path}", mirrorExePath);

        var mainWindow = Application.Current.MainWindow;
        if (mainWindow == null)
        {
            Log.Logger.Error("Could not get main window");
            return;
        }

        var handle = new WindowInteropHelper(mainWindow).Handle;
        var onlyMMonitor = _monitorsService.GetMonitorForWindowHandle(handle);
        var mediaMonitor = _monitorsService.GetSystemMonitor(_optionsService.MediaMonitorId);

        if (onlyMMonitor?.MonitorId == null || mediaMonitor?.MonitorId == null)
        {
            Log.Logger.Error("Cannot get monitor - unable to display mirror");
            return;
        }

        if (mediaMonitor.MonitorId.Equals(onlyMMonitor.MonitorId, StringComparison.Ordinal))
        {
            Log.Logger.Error("Cannot display mirror since OnlyM and Media window share a monitor");
            return;
        }

        Log.Logger.Debug("Main monitor = {Device}", onlyMMonitor.Monitor?.DeviceName);
        Log.Logger.Debug("Media monitor = {Device}", mediaMonitor.Monitor?.DeviceName);

        StatusEvent?.Invoke(this, new WebBrowserProgressEventArgs { Description = Properties.Resources.LAUNCHING_MIRROR });

        try
        {
            var zoomStr = _optionsService.MirrorZoom.ToString(CultureInfo.InvariantCulture);
            var hotKey = _optionsService.MirrorHotKey;

            var commandLineArgs =
                $"{onlyMMonitor.Monitor?.DeviceName} {mediaMonitor.Monitor?.DeviceName} {zoomStr} {hotKey}";

            Log.Logger.Debug("Starting mirror exe with args = {Args}", commandLineArgs);

            _mirrorProcess = new Process
            {
                StartInfo =
                {
                    FileName = mirrorExePath,
                    Arguments = commandLineArgs,
                },
                EnableRaisingEvents = true,
            };

            _mirrorProcess.Exited += HandleMirrorProcessExited;

            if (!_mirrorProcess.Start())
            {
                EventTracker.Error("Could not launch mirror executable");
                Log.Logger.Error("Could not launch mirror");
            }
        }
        catch (Exception ex)
        {
            EventTracker.Error(ex, "Launching mirror");
            Log.Logger.Error(ex, "Could not launch mirror");
        }
        finally
        {
            await Task.Delay(1000);
            await Application.Current.Dispatcher.InvokeAsync(()
                => StatusEvent?.Invoke(this, new WebBrowserProgressEventArgs { Description = string.Empty }));
        }
    }

    public void CloseMirror()
    {
        if (!Mutex.TryOpenExisting("OnlyMMirrorMutex", out var _))
        {
            return;
        }

        if (_mirrorProcess == null)
        {
            return;
        }

        try
        {
            if (_mirrorProcess.CloseMainWindow())
            {
                _mirrorProcess = null;
            }
        }
        catch (Exception ex)
        {
            EventTracker.Error(ex, "Closing mirror");
            Log.Logger.Error(ex, "Could not close mirror");
        }
    }

    private async Task InitBrowserFromDatabase(string url)
    {
        SetZoomLevel(0.0);

        try
        {
            var browserData = _databaseService.GetBrowserData(url);
            if (browserData != null)
            {
                SetZoomLevel(browserData.ZoomLevel);

                // this hack to allow the web renderer time to change zoom level before fading in!
                await Task.Delay(500);
            }
        }
        catch (Exception ex)
        {
            EventTracker.Error(ex, "Getting browser data from database");
            Log.Logger.Error(ex, "Could not get browser data from database");
        }
    }

    private void SetZoomLevel(double zoomLevel)
    {
        // don't understand why this apparent duplication is necessary!
        _browser.SetZoomLevel(zoomLevel);
        _browser.ZoomLevel = zoomLevel;
    }

    private void UpdateBrowserDataInDatabase()
    {
        if (_currentMediaItemUrl == null)
        {
            return;
        }

        try
        {
            _databaseService.AddBrowserData(_currentMediaItemUrl, _browser.ZoomLevel);
        }
        catch (Exception ex)
        {
            EventTracker.Error(ex, "Updating browser data in database");
            Log.Logger.Error(ex, "Could not update browser data in database");
        }
    }

    private static MediaEventArgs CreateMediaEventArgs(Guid id, MediaChange change) =>
        new()
        {
            MediaItemId = id,
            Classification = MediaClassification.Web,
            Change = change,
        };

    private void OnMediaChangeEvent(MediaEventArgs e) => MediaChangeEvent?.Invoke(this, e);

    private void HandleBrowserLoadingStateChanged(object? sender, LoadingStateChangedEventArgs e)
    {
        if (e.IsLoading)
        {
            StatusEvent?.Invoke(this, new WebBrowserProgressEventArgs
            {
                Description = Properties.Resources.WEB_LOADING,
            });
        }
        else
        {
            StatusEvent?.Invoke(this, new WebBrowserProgressEventArgs { Description = string.Empty });
        }

        Application.Current.Dispatcher.Invoke(async () =>
        {
            if (e.IsLoading)
            {
                Log.Debug("Loading web page = {Address}", _browser.Address);
            }
            else
            {
                Log.Debug("Loaded web page");
            }

            if (!e.IsLoading && !_showing && _currentMediaItemUrl != null)
            {
                // page is loaded so fade in...
                _showing = true;
                await InitBrowserFromDatabase(_currentMediaItemUrl);

                FadeBrowser(true, () =>
                {
                    OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Started));

                    if (_useMirror)
                    {
                        _ = ShowMirrorAsync().ContinueWith(
                            _ => SetFocusToBrowserGrid(), TaskScheduler.FromCurrentSynchronizationContext());
                    }
                    else
                    {
                        _browserGrid.Focus();
                    }
                });
            }
        });
    }

#pragma warning disable SYSLIB1054
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
#pragma warning restore SYSLIB1054

    private void SetFocusToBrowserGrid()
    {
        Application.Current.Dispatcher.BeginInvoke(
            () =>
            {
                // Set focus back to the browser grid after the mirror is shown and UI is idle
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow != null)
                {
                    SetForegroundWindow(new WindowInteropHelper(mainWindow).Handle);
                }

                _browserGrid.Focus();
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private void FadeBrowser(bool fadeIn, Action completed)
    {
        var fadeTimeSecs = 1.0;

        if (fadeIn)
        {
            // note that the fade in time is longer than fade out - just seems to look better
            fadeTimeSecs *= 1.2;
        }

        var animation = new DoubleAnimation
        {
            Duration = TimeSpan.FromSeconds(fadeTimeSecs * 1.2),
            From = fadeIn ? 0.0 : 1.0,
            To = fadeIn ? 1.0 : 0.0,
        };

        animation.Completed += (_, _) => completed();
        _browserGrid.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    private void RemoveAnimation()
    {
        _browserGrid.BeginAnimation(UIElement.OpacityProperty, null);
        _browserGrid.Opacity = 0.0;
    }

    private void InitBrowser()
    {
        _browser.LoadingStateChanged += HandleBrowserLoadingStateChanged;
        _browser.LoadError += HandleBrowserLoadError;
        _browser.StatusMessage += HandleBrowserStatusMessage;
        _browser.FrameLoadStart += HandleBrowserFrameLoadStart;

        _browser.LifeSpanHandler = new BrowserLifeSpanHandler();
    }

    private void HandleBrowserStatusMessage(object? sender, StatusMessageEventArgs e)
    {
        if (!e.Browser.IsLoading)
        {
            StatusEvent?.Invoke(this, new WebBrowserProgressEventArgs { Description = e.Value });
        }
    }

    private void HandleBrowserFrameLoadStart(object? sender, FrameLoadStartEventArgs e)
    {
#pragma warning disable CA1863
        var s = string.Format(CultureInfo.CurrentCulture, Properties.Resources.LOADING_FRAME, e.Frame.Identifier);
#pragma warning restore CA1863
        StatusEvent?.Invoke(this, new WebBrowserProgressEventArgs { Description = s });
    }

    private void HandleBrowserLoadError(object? sender, LoadErrorEventArgs e)
    {
        // Don't display an error for downloaded files where the user aborted the download.
        if (e.ErrorCode == CefErrorCode.Aborted)
        {
            return;
        }

#pragma warning disable CA1863
        var errorMsg = string.Format(CultureInfo.CurrentCulture, Properties.Resources.WEB_LOAD_FAIL, e.FailedUrl, e.ErrorText, e.ErrorCode);
#pragma warning restore CA1863
        var body = $"<html><body><h2>{errorMsg}</h2></body></html>";

        _browser.LoadHtml(body, e.FailedUrl);
    }

    private void HandleMirrorProcessExited(object? sender, EventArgs e)
    {
        if (_mirrorProcess == null)
        {
            return;
        }

        if (_mirrorProcess.ExitCode == 0)
        {
            Log.Logger.Debug("Mirror process closed normally");
        }
        else
        {
            Log.Logger.Error("Mirror process exited with exit code {ExitCode}", _mirrorProcess.ExitCode);

            if (_mirrorProcess.ExitCode == 5)
            {
                _snackbarService.EnqueueWithOk(Properties.Resources.CHANGE_MIRROR_HOTKEY, Properties.Resources.OK);
            }
        }
    }

    private static string GetPdfViewString(PdfViewStyle pdfViewStyle)
    {
        switch (pdfViewStyle)
        {
            case PdfViewStyle.HorizontalFit:
                return "FitH";
            case PdfViewStyle.VerticalFit:
                return "FitV";

            default:
            case PdfViewStyle.Default:
                return string.Empty;
        }
    }

    private static string? GetCurrentSolutionFolderWhenDebugging()
    {
        var workingDirectory = Environment.CurrentDirectory;
        return Directory.GetParent(workingDirectory)?.Parent?.Parent?.Parent?.FullName;
    }

    private static string? GetMirrorExePath()
    {
        const string mirrorExeFileName = "OnlyMMirror.exe";

        // ReSharper disable once JoinDeclarationAndInitializer
        string? mirrorExePath = null;

#if DEBUG
        var solutionFolder = GetCurrentSolutionFolderWhenDebugging();
        if (solutionFolder != null)
        {
            var folder = Path.Combine(solutionFolder, "Debug");
            if (!Directory.Exists(folder))
            {
                Log.Logger.Error("Could not find OnlyMMirror bin folder");
                return null;
            }

            mirrorExePath = Path.Combine(folder, mirrorExeFileName);
        }
#else
        var folder = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly()?.Location);
        if (folder == null)
        {
            Log.Logger.Error("Could not get assembly folder");
            return null;
        }
        
        mirrorExePath = Path.Combine(folder, mirrorExeFileName);
#endif

        if (!File.Exists(mirrorExePath))
        {
            Log.Logger.Error($"Could not find {mirrorExeFileName}");
            return null;
        }

        return mirrorExePath;
    }
}
