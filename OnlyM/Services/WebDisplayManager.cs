namespace OnlyM.Services
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Interop;
    using System.Windows.Media.Animation;
    using CefSharp;
    using CefSharp.Wpf;
    using GalaSoft.MvvmLight.Threading;
    using OnlyM.Core.Models;
    using OnlyM.Core.Services.Database;
    using OnlyM.Core.Services.Monitors;
    using OnlyM.Core.Services.Options;
    using OnlyM.Core.Services.WebShortcuts;
    using OnlyM.CoreSys.Services.Snackbar;
    using OnlyM.Models;
    using OnlyM.Services.WebBrowser;
    using Serilog;

    internal sealed class WebDisplayManager
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
        private string _currentMediaItemUrl;
        private Process _mirrorProcess;

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

        public event EventHandler<MediaEventArgs> MediaChangeEvent;

        public event EventHandler<WebBrowserProgressEventArgs> StatusEvent;

        public void ShowWeb(
            string mediaItemFilePath,
            Guid mediaItemId,
            int pdfStartingPage,
            PdfViewStyle pdfViewStyle,
            bool showMirror,
            ScreenPosition screenPosition)
        {
            _useMirror = showMirror;

            if (string.IsNullOrEmpty(mediaItemFilePath))
            {
                return;
            }

            _showing = false;
            _mediaItemId = mediaItemId;

            ScreenPositionHelper.SetScreenPosition(_browserGrid, screenPosition);

            OnMediaChangeEvent(CreateMediaEventArgs(mediaItemId, MediaChange.Starting));

            string webAddress;
            if (Path.GetExtension(mediaItemFilePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                // https://www.adobe.com/content/dam/acom/en/devnet/acrobat/pdfs/pdf_open_parameters.pdf

                var viewString = GetPdfViewString(pdfViewStyle);
                var cacheBusterString = DateTime.Now.Ticks.ToString();

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

        public void ShowMirror()
        {
            Log.Logger.Debug("Attempting to open mirror");

            if (Mutex.TryOpenExisting("OnlyMMirrorMutex", out var _))
            {
                Log.Logger.Debug("OnlyMMirrorMutex mutex exists");
                return;
            }

            var folder = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            if (folder == null)
            {
                Log.Logger.Error("Could not get assembly folder");
                return;
            }

            const string mirrorExeFileName = "OnlyMMirror.exe";

            var mirrorExePath = Path.Combine(folder, mirrorExeFileName);
            if (!File.Exists(mirrorExePath))
            {
                Log.Logger.Error($"Could not find {mirrorExeFileName}");
                return;
            }

            Log.Logger.Debug($"Mirror path = {mirrorExePath}");

            var mainWindow = Application.Current.MainWindow;
            if (mainWindow == null)
            {
                Log.Logger.Error("Could not get main window");
                return;
            }

            var handle = new WindowInteropHelper(mainWindow).Handle;
            var onlyMMonitor = _monitorsService.GetMonitorForWindowHandle(handle);
            var mediaMonitor = _monitorsService.GetSystemMonitor(_optionsService.MediaMonitorId);

            if (onlyMMonitor == null || mediaMonitor == null)
            {
                Log.Logger.Debug("Cannot get monitor");
                return;
            }

            if (mediaMonitor.MonitorId.Equals(onlyMMonitor.MonitorId))
            {
                Log.Logger.Error("Cannot display mirror since OnlyM and Media window share a monitor");
                return;
            }

            Log.Logger.Debug($"Main monitor = {onlyMMonitor.Monitor.DeviceName}");
            Log.Logger.Debug($"Media monitor = {mediaMonitor.Monitor.DeviceName}");

            StatusEvent?.Invoke(this, new WebBrowserProgressEventArgs { Description = Properties.Resources.LAUNCHING_MIRROR });

            try
            {
                var zoomStr = _optionsService.MirrorZoom.ToString(CultureInfo.InvariantCulture);
                var hotKey = _optionsService.MirrorHotKey;

                var commandLineArgs =
                    $"{onlyMMonitor.Monitor.DeviceName} {mediaMonitor.Monitor.DeviceName} {zoomStr} {hotKey}";

                Log.Logger.Debug($"Starting mirror exe with args = {commandLineArgs}");

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
                    Log.Logger.Error("Could not launch mirror");
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Could not launch mirror");
            }
            finally
            {
                Task.Delay(1000).ContinueWith(t =>
                {
                    DispatcherHelper.CheckBeginInvokeOnUI(() =>
                    {
                        StatusEvent?.Invoke(this, new WebBrowserProgressEventArgs { Description = string.Empty });
                    });
                });
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
            try
            {
                _databaseService.AddBrowserData(_currentMediaItemUrl, _browser.ZoomLevel);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Could not update browser data in database");
            }
        }

        private MediaEventArgs CreateMediaEventArgs(Guid id, MediaChange change)
        {
            return new MediaEventArgs
            {
                MediaItemId = id,
                Classification = MediaClassification.Web,
                Change = change,
            };
        }

        private void OnMediaChangeEvent(MediaEventArgs e)
        {
            MediaChangeEvent?.Invoke(this, e);
        }

        private void HandleBrowserLoadingStateChanged(object sender, CefSharp.LoadingStateChangedEventArgs e)
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

            DispatcherHelper.CheckBeginInvokeOnUI(async () =>
            {
                Log.Debug(e.IsLoading ? $"Loading web page = {_browser.Address}" : "Loaded web page");

                if (!e.IsLoading && !_showing)
                {
                    // page is loaded so fade in...
                    _showing = true;
                    await InitBrowserFromDatabase(_currentMediaItemUrl);

                    FadeBrowser(true, () =>
                    {
                        OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Started));
                        _browserGrid.Focus();

                        if (_useMirror)
                        {
                            ShowMirror();
                        }
                    });
                }
            });
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

            if (completed != null)
            {
                animation.Completed += (sender, args) => { completed(); };
            }

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

        private void HandleBrowserStatusMessage(object sender, StatusMessageEventArgs e)
        {
            if (!e.Browser.IsLoading)
            {
                StatusEvent?.Invoke(this, new WebBrowserProgressEventArgs { Description = e.Value });
            }
        }

        private void HandleBrowserFrameLoadStart(object sender, FrameLoadStartEventArgs e)
        {
            var s = string.Format(Properties.Resources.LOADING_FRAME, e.Frame.Identifier);
            StatusEvent?.Invoke(this, new WebBrowserProgressEventArgs { Description = s });
        }

        private void HandleBrowserLoadError(object sender, LoadErrorEventArgs e)
        {
            // Don't display an error for downloaded files where the user aborted the download.
            if (e.ErrorCode == CefErrorCode.Aborted)
            {
                return;
            }

            var errorMsg = string.Format(Properties.Resources.WEB_LOAD_FAIL, e.FailedUrl, e.ErrorText, e.ErrorCode);
            var body = $"<html><body><h2>{errorMsg}</h2></body></html>";

            _browser.LoadHtml(body, e.FailedUrl);
        }

        private void HandleMirrorProcessExited(object sender, EventArgs e)
        {
            if (_mirrorProcess.ExitCode == 0)
            {
                Log.Logger.Debug("Mirror process closed normally");
            }
            else
            {
                Log.Logger.Error($"Mirror process exited with exit code {_mirrorProcess.ExitCode}");

                if (_mirrorProcess.ExitCode == 5)
                {
                    _snackbarService.EnqueueWithOk(Properties.Resources.CHANGE_MIRROR_HOTKEY, Properties.Resources.OK);
                }
            }
        }

        private string GetPdfViewString(PdfViewStyle pdfViewStyle)
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
    }
}
