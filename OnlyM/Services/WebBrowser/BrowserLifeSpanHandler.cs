using CefSharp;

namespace OnlyM.Services.WebBrowser
{
    public class BrowserLifeSpanHandler : ILifeSpanHandler
    {
        public bool OnBeforePopup(
            IWebBrowser chromiumWebBrowser, 
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
            out IWebBrowser? newBrowser)
        {
            frame.LoadUrl(targetUrl);
            newBrowser = null;
            return true;
        }

        public void OnAfterCreated(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            // nothing
        }

        public bool DoClose(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            return false;
        }

        public void OnBeforeClose(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            // nothing
        }
    }
}
