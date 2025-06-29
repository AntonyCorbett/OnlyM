using System;
using System.IO;
using OnlyM.CoreSys;
using OnlyM.Models;
using Serilog;

namespace OnlyM.Services;

internal static class SubtitleFileGenerator
{
    public static event EventHandler<SubtitleFileEventArgs>? SubtitleFileEvent;

    public static string? Generate(string mediaItemFilePath, Guid mediaItemId)
    {
        try
        {
            Log.Logger.Debug($"Generating subtitle file for media {mediaItemFilePath}");

            var destFolder = Path.GetDirectoryName(mediaItemFilePath);
            if (destFolder == null)
            {
                return null;
            }

            var srtFileName = Path.GetFileNameWithoutExtension(mediaItemFilePath);

            var videoFileInfo = new FileInfo(mediaItemFilePath);
            if (!videoFileInfo.Exists)
            {
                return null;
            }

            var srtFile = Path.Combine(destFolder, Path.ChangeExtension(srtFileName, ".srt"));
            if (ShouldCreate(srtFile, videoFileInfo.CreationTimeUtc))
            {
                var ffmpegFolder = Unosquare.FFME.Library.FFmpegDirectory;

                SubtitleFileEvent?.Invoke(null, new SubtitleFileEventArgs { MediaItemId = mediaItemId, Starting = true });

                if (!GraphicsUtils.GenerateSubtitleFile(
                        ffmpegFolder,
                        mediaItemFilePath,
                        srtFile))
                {
                    return null;
                }

                File.SetCreationTimeUtc(srtFile, videoFileInfo.CreationTimeUtc);

                SubtitleFileEvent?.Invoke(null, new SubtitleFileEventArgs { MediaItemId = mediaItemId, Starting = false });
            }

            return srtFile;
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, $"Could not create srt file for media: {mediaItemFilePath}");
            return null;
        }
    }

    private static bool ShouldCreate(string srtFile, DateTime videoFileCreationTimeUtc)
    {
        if (!File.Exists(srtFile))
        {
            return true;
        }

        var fileInfo = new FileInfo(srtFile);
        if (fileInfo.CreationTimeUtc != videoFileCreationTimeUtc)
        {
            Log.Logger.Debug("Old subtitle file found");

            // we also update the subtitles file if it looks
            // like the video has been changed
            return true;
        }

        Log.Logger.Debug("Subtitle file already exists");

        return false;
    }
}
