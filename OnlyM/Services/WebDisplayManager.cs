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
    using Serilog;

    internal sealed class WebDisplayManager
    {
        private const string BlankUrl = @"about:blank";
        private readonly ChromiumWebBrowser _browser;
        private Guid _mediaItemId;

        public WebDisplayManager(ChromiumWebBrowser browser)
        {
            _browser = browser;
            _browser.LoadingStateChanged += HandleBrowserLoadingStateChanged;
        }

        public event EventHandler<MediaEventArgs> MediaChangeEvent;

        public void ShowWeb(string mediaItemFilePath, Guid mediaItemId)
        {
            _mediaItemId = mediaItemId;

            OnMediaChangeEvent(CreateMediaEventArgs(mediaItemId, MediaChange.Starting));

            RemoveAnimation();

            var urlHelper = new WebShortcutHelper(mediaItemFilePath);
            _browser.Address = urlHelper.Uri;
        }

        public void HideWeb()
        {
            OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Stopping));

            RemoveAnimation();

            FadeBrowser(false, () =>
            {
                _browser.Address = BlankUrl;
                OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Stopped));
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
                if (_browser.Address == BlankUrl)
                {
                    return;
                }

                Log.Debug(e.IsLoading ? $"Loading web page = {_browser.Address}" : "Loaded web page");

                if (!e.IsLoading)
                {
                    // page is loaded so fade in...
                    FadeBrowser(true, () =>
                    {
                        OnMediaChangeEvent(CreateMediaEventArgs(_mediaItemId, MediaChange.Started));
                    });
                }
            });
        }

        private void FadeBrowser(bool fadeIn, Action completed)
        {
            double fadeTimeSecs = 1.0;

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

            _browser.BeginAnimation(UIElement.OpacityProperty, animation);
        }

        private void RemoveAnimation()
        {
            _browser.BeginAnimation(UIElement.OpacityProperty, null);
            _browser.Opacity = 0.0;
        }
    }
}
