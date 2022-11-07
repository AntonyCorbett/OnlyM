using System.Net;
using System.Text;

namespace OnlyM.Core.Utils
{
    public static class WebUtils
    {
        private const string UserAgent = "SoundBox (+https://soundboxsoftware.com)";

        public static WebClient CreateWebClient()
        {
            // todo: update to HttpClient, ensuring we use sync rather than async to avoid multiple code changes

#pragma warning disable SYSLIB0014 // Type or member is obsolete
            var wc = new WebClient { Encoding = Encoding.UTF8 };
#pragma warning restore SYSLIB0014 // Type or member is obsolete
            wc.Headers.Add("user-agent", UserAgent);
            return wc;
        }
    }
}
