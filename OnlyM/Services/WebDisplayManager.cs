namespace OnlyM.Services
{
    using System;
    using System.Windows;
    using System.Windows.Media.Animation;
    using CefSharp.Wpf;
    using GalaSoft.MvvmLight.Threading;
    using OnlyM.Core.Models;
    using OnlyM.Core.Services.WebShortcuts;
    using OnlyM.Models;
    using OnlyM.Services.WebBrowser;
    using Serilog;

    internal sealed class WebDisplayManager
    {
        private readonly ChromiumWebBrowser _browser;
        private readonly FrameworkElement _browserGrid;
        private Guid _mediaItemId;
        private bool _showing;

        public WebDisplayManager(ChromiumWebBrowser browser, FrameworkElement browserGrid)
        {
            _browser = browser;
            _browserGrid = browserGrid;

            InitBrowser();
        }

        public event EventHandler<MediaEventArgs> MediaChangeEvent;

        public void ShowWeb(string mediaItemFilePath, Guid mediaItemId)
        {
            _showing = false;
            _mediaItemId = mediaItemId;

            OnMediaChangeEvent(CreateMediaEventArgs(mediaItemId, MediaChange.Starting));

            RemoveAnimation();
            
            _browserGrid.Visibility = Visibility.Visible;
            
            var urlHelper = new WebShortcutHelper(mediaItemFilePath);
            _browser.Load(urlHelper.Uri);
        }

        public void HideWeb()
        {
            OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Stopping));

            RemoveAnimation();

            FadeBrowser(false, () =>
            {
                OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Stopped));
                _browserGrid.Visibility = Visibility.Hidden;
            });
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
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                Log.Debug(e.IsLoading ? $"Loading web page = {_browser.Address}" : "Loaded web page");

                if (!e.IsLoading)
                {
                    if (!_showing)
                    {
                        // page is loaded so fade in...
                        _showing = true;
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
            _browser.LifeSpanHandler = new BrowserLifeSpanHandler();
        }
    }
}
