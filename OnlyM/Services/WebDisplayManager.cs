using GalaSoft.MvvmLight.Threading;

namespace OnlyM.Services
{
    using System;
    using CefSharp.Wpf;
    using OnlyM.Core.Services.WebShortcuts;
    using Serilog;

    internal sealed class WebDisplayManager
    {
        private readonly ChromiumWebBrowser _browser;

        public WebDisplayManager(ChromiumWebBrowser browser)
        {
            _browser = browser;
            _browser.LoadingStateChanged += HandleBrowserLoadingStateChanged;
        }

        public void ShowWeb(string mediaItemFilePath)
        {
            var urlHelper = new WebShortcutHelper(mediaItemFilePath);
            
            _browser.Address = urlHelper.Uri;
        }

        private void HandleBrowserLoadingStateChanged(object sender, CefSharp.LoadingStateChangedEventArgs e)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                Log.Debug(e.IsLoading ? $"Loading web page = {_browser.Address}" : "Loaded web page");
            });
        }

        public void HideWeb()
        {
            _browser.Address = null;
        }
    }
}
