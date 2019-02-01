namespace OnlyM.Core.Utils
{
    using System.Net;
    using System.Text;

    public static class WebUtils
    {
        private const string UserAgent = "SoundBox (+https://soundboxsoftware.com)";

        public static WebClient CreateWebClient()
        {
            var wc = new WebClient { Encoding = Encoding.UTF8 };
            wc.Headers.Add("user-agent", UserAgent);
            return wc;
        }
    }
}
