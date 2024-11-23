using CefSharp;

namespace OnlyM.Services.WebBrowser;

public class PdfSchemeHandlerFactory : ISchemeHandlerFactory
{
    public const string SchemeName = "pdf";

    public IResourceHandler Create(IBrowser browser, IFrame frame, string schemeName, IRequest request) =>
        new PdfSchemeHandler();
}
