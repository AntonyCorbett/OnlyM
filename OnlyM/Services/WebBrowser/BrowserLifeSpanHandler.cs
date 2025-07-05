using CefSharp;

namespace OnlyM.Services.WebBrowser;

// BrowserLifeSpanHandler is here to prevent popups from opening new windows and
// instead loads their URLs in the current browser frame. This is useful in
// embedded browser scenarios where you want to keep navigation within a
// single window and avoid multiple browser instances.

public class BrowserLifeSpanHandler : ILifeSpanHandler
{
    // This method is called when a popup (e.g., a new window or tab) is about to be created.
    // Instead of allowing a new popup window, we load the target URL in the current frame
    // (frame.LoadUrl(targetUrl);), set newBrowser to null, and return true to cancel the popup.
    // Effect: All popup requests are suppressed and redirected to the current browser frame.
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

    // we prevent the browser being closed by this handler
    public bool DoClose(IWebBrowser chromiumWebBrowser, IBrowser browser) => false;

    public void OnBeforeClose(IWebBrowser chromiumWebBrowser, IBrowser browser)
    {
        // nothing
    }
}
