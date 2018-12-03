namespace OnlyM.Services
{
    using System;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Media.Animation;
    using CefSharp;
    using CefSharp.Wpf;
    using GalaSoft.MvvmLight.Threading;
    using OnlyM.Core.Models;
    using OnlyM.Core.Services.Database;
    using OnlyM.Core.Services.WebShortcuts;
    using OnlyM.Models;
    using OnlyM.Services.WebBrowser;
    using Serilog;

    internal sealed class WebDisplayManager
    {
        private readonly ChromiumWebBrowser _browser;
        private readonly FrameworkElement _browserGrid;
        private readonly IDatabaseService _databaseService;
        private Guid _mediaItemId;
        private bool _showing;
        private string _currentMediaItemUrl;

        public WebDisplayManager(
            ChromiumWebBrowser browser, 
            FrameworkElement browserGrid,
            IDatabaseService databaseService)
        {
            _browser = browser;
            _browserGrid = browserGrid;
            _databaseService = databaseService;

            InitBrowser();
        }

        public event EventHandler<MediaEventArgs> MediaChangeEvent;

        public event EventHandler<WebBrowserProgressEventArgs> StatusEvent;

        public void ShowWeb(string mediaItemFilePath, Guid mediaItemId)
        {
            _showing = false;
            _mediaItemId = mediaItemId;

            OnMediaChangeEvent(CreateMediaEventArgs(mediaItemId, MediaChange.Starting));

            var urlHelper = new WebShortcutHelper(mediaItemFilePath);
            _currentMediaItemUrl = urlHelper.Uri;

            RemoveAnimation();
            
            _browserGrid.Visibility = Visibility.Visible;
            
            _browser.Load(urlHelper.Uri);
        }

        public void HideWeb(string mediaItemFilePath)
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
                Change = change
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
                // todo: localise
                StatusEvent?.Invoke(this, new WebBrowserProgressEventArgs { Description = "Loading..." });
            }
            else
            {
                StatusEvent?.Invoke(this, new WebBrowserProgressEventArgs { Description = string.Empty });
            }

            DispatcherHelper.CheckBeginInvokeOnUI(async () =>
            {
                Log.Debug(e.IsLoading ? $"Loading web page = {_browser.Address}" : "Loaded web page");

                if (!e.IsLoading)
                {
                    if (!_showing)
                    {
                        // page is loaded so fade in...
                        _showing = true;
                        await InitBrowserFromDatabase(_currentMediaItemUrl);

                        FadeBrowser(true, () =>
                        {
                            OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Started));
                            _browserGrid.Focus();
                        });
                    }
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
                To = fadeIn ? 1.0 : 0.0
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
                StatusEvent?.Invoke(this, new WebBrowserProgressEventArgs {Description = e.Value});
            }
        }

        private void HandleBrowserFrameLoadStart(object sender, FrameLoadStartEventArgs e)
        {
            // todo: localise
            var s = $"Loading frame {e.Frame.Identifier}";
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
    }
}
