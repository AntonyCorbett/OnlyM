using System.IO;

namespace OnlyM.Core.Services.Media
{
    using System;
    using Models;
    using Serilog;

    // ReSharper disable once ClassNeverInstantiated.Global
    public class MediaMetaDataService : IMediaMetaDataService
    {
        public MediaMetaData GetMetaData(
            string mediaItemFilePath, 
            SupportedMediaType mediaType,
            string ffmpegFolder)
        {
            try
            {
                return mediaType.Classification == MediaClassification.Video 
                    ? GetVideoMetaData(mediaItemFilePath, ffmpegFolder) 
                    : GetNonVideoMetaData(mediaItemFilePath);
            }
            catch (System.IO.IOException)
            {
                Log.Logger.Error($"Could not get metadata from file: {mediaItemFilePath} (in use)");
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, $"Could not get metadata from file: {mediaItemFilePath}");
            }

            return null;
        }

        private string StripNewLines(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }

            return s.Replace("\r\n", " ").Replace("\n", " ");
        }

        private MediaMetaData GetVideoMetaData(string mediaItemFilePath, string ffmpegFolder)
        {
            Unosquare.FFME.MediaEngine.FFmpegDirectory = ffmpegFolder;
            Unosquare.FFME.MediaEngine.LoadFFmpeg();

            var info = Unosquare.FFME.MediaEngine.RetrieveMediaInfo(mediaItemFilePath);

            string title = null;
            info.Metadata?.TryGetValue("title", out title);

            if (string.IsNullOrEmpty(title))
            {
                title = Path.GetFileNameWithoutExtension(mediaItemFilePath);
            }
        
            return new MediaMetaData
            {
                Title = StripNewLines(title),
                Duration = info.Duration
            };
        }

        private MediaMetaData GetNonVideoMetaData(string mediaItemFilePath)
        {
            using (var tf = TagLib.File.Create(mediaItemFilePath))
            {
                tf.Mode = TagLib.File.AccessMode.Read;

                return new MediaMetaData
                {
                    Title = StripNewLines(tf.Tag?.Title),
                    Duration = tf.Properties?.Duration ?? TimeSpan.Zero
                };
            }
        }
    }
}
