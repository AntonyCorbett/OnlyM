using System;
using System.Text;

namespace OnlyM.Core.Utils
{
    public static class FFmpegUtils
    {
        // Jul 2021 - this FFMPEG issue has been fixed so the workaround is no longer needed (hence the commenting out below).

        // there is an issue in ffmpeg (https://trac.ffmpeg.org/ticket/819)
        // - it doesn't accept some file names
        public static Uri FixUnicodeUri(Uri uri)
        {
            return uri;
            //return new Uri(FixUnicodeString(uri.ToString()));
        }

        public static string FixUnicodeString(string s)
        {
            return s;
            //return Encoding.GetEncoding(0).GetString(Encoding.UTF8.GetBytes(s));
        }
    }
}
