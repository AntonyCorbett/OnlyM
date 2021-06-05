namespace OnlyM.Services.WebBrowser
{
    using CefSharp;

    public class PdfSchemeHandlerFactory : ISchemeHandlerFactory
    {
        public const string SchemeName = "pdf";

        public IResourceHandler Create(IBrowser browser, IFrame frame, string schemeName, IRequest request)
        {
            return new PdfSchemeHandler();
        }
    }
}
