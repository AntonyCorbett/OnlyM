namespace OnlyM.Core.Utils
{
    using System;
    using System.Text;

    public static class FFmpegUtils
    {
        public static Uri FixUnicodeUri(Uri uri)
        {
            return new Uri(FixUnicodeString(uri.ToString()));
        }

        public static string FixUnicodeString(string s)
        {
            return Encoding.GetEncoding(1252).GetString(Encoding.UTF8.GetBytes(s));
        }
    }
}
