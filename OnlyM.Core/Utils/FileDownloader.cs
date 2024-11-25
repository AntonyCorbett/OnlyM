using System;
using System.IO;
using Serilog;

namespace OnlyM.Core.Utils;

public static class FileDownloader
{
    public static bool Download(Uri remoteUri, string localFile, bool overwrite)
    {
        if (File.Exists(localFile) && !overwrite)
        {
            return false;
        }

        try
        {
            using var wc = WebUtils.CreateWebClient();
            wc.DownloadFile(remoteUri, localFile);
            return true;
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, $"Could not download {remoteUri}");
        }

        return false;
    }
}
