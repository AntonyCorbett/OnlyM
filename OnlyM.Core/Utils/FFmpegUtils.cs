namespace OnlyM.Core.Utils
{
    using System;
    using System.Text;

    public static class FFmpegUtils
    {
        // there is an issue in ffmpeg (https://trac.ffmpeg.org/ticket/819)
        // - it doesn't accept some file names
        public static Uri FixUnicodeUri(Uri uri)
        {
            return new Uri(FixUnicodeString(uri.ToString()));
        }

        public static string FixUnicodeString(string s)
        {
            return Encoding.GetEncoding(0).GetString(Encoding.UTF8.GetBytes(s));
        }
    }
}
