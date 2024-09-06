using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using CefSharp;

namespace OnlyM.Services.WebBrowser
{
    internal sealed class PdfSchemeHandler : ResourceHandler
    {
        public override CefReturnValue ProcessRequestAsync(IRequest request, ICallback callback)
        {
            var uri = new Uri(request.Url);
            
            var file = $"{uri.Host}:{uri.LocalPath}";
         
            Task.Run(() =>
            {
                using (callback)
                {
                    if (!File.Exists(file))
                    {
                        callback.Cancel();
                        return;
                    }

                    var bytes = File.ReadAllBytes(file);

                    var stream = new MemoryStream(bytes) { Position = 0 };
                    ResponseLength = stream.Length;

                    var fileExtension = Path.GetExtension(file);
                    MimeType = GetMimeType(fileExtension);
                    StatusCode = (int)HttpStatusCode.OK;
                    Stream = stream;

                    callback.Continue();
                }
            });

            return CefReturnValue.ContinueAsync;
        }
    }
}
