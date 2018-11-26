namespace OnlyM.Services
{
    using System;
    using CefSharp.Wpf;

    internal sealed class WebDisplayManager
    {
        private readonly ChromiumWebBrowser _browser;

        public WebDisplayManager(ChromiumWebBrowser browser)
        {
            _browser = browser;
        }

        public void ShowWeb(string mediaItemFilePath)
        {
            ////_browser.Address = webAddress.ToString();
        }

        public void Hide()
        {
            _browser.Address = null;
        }
    }
}
