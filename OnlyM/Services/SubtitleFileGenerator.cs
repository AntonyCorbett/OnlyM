namespace OnlyM.Services
{
    using System;
    using System.IO;
    using Core.Utils;

    internal static class SubtitleFileGenerator
    {
        public static string Generate(string mediaItemFilePath)
        {
            var ffmpegFolder = Unosquare.FFME.MediaElement.FFmpegDirectory;

            var destFolder = Path.GetDirectoryName(mediaItemFilePath);
            if (destFolder == null)
            {
                return null;
            }

            var srtFileName = Path.GetFileNameWithoutExtension(mediaItemFilePath);
            if (srtFileName == null)
            {
                return null;
            }

            var videoFileInfo = new FileInfo(mediaItemFilePath);
            if (!videoFileInfo.Exists)
            {
                return null;
            }

            var srtFile = Path.Combine(destFolder, Path.ChangeExtension(srtFileName, ".srt"));
            if (ShouldCreate(srtFile, videoFileInfo.CreationTimeUtc))
            {
                if (!GraphicsUtils.GenerateSubtitleFile(
                    ffmpegFolder,
                    mediaItemFilePath,
                    srtFile))
                {
                    return null;
                }

                File.SetCreationTimeUtc(srtFile, videoFileInfo.CreationTimeUtc);
            }

            return srtFile;
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
                // we also update the subtitles file if it looks
                // like the video has been changed
                return true;
            }

            return false;
        }
    }
}
