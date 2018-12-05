namespace OnlyM.Services.WebBrowser
{
    using CefSharp;

    public class BrowserLifeSpanHandler : ILifeSpanHandler
    {
        public bool OnBeforePopup(
            IWebBrowser browserControl, 
            IBrowser browser, 
            IFrame frame, 
            string targetUrl, 
            string targetFrameName,
            WindowOpenDisposition targetDisposition, 
            bool userGesture, 
            IPopupFeatures popupFeatures, 
            IWindowInfo windowInfo,
            IBrowserSettings browserSettings, 
            ref bool noJavascriptAccess, 
            out IWebBrowser newBrowser)
        {
            frame.LoadUrl(targetUrl);
            newBrowser = null;
            return true;
        }

        public void OnAfterCreated(IWebBrowser browserControl, IBrowser browser)
        {
            // nothing
        }

        public bool DoClose(IWebBrowser browserControl, IBrowser browser)
        {
            return false;
        }

        public void OnBeforeClose(IWebBrowser browserControl, IBrowser browser)
        {
            // nothing
        }
    }
}
