namespace OnlyM.Core.Services.Media
{
    using System;
    using Models;
    using Serilog;

    // ReSharper disable once ClassNeverInstantiated.Global
    public class MediaMetaDataService : IMediaMetaDataService
    {
        public MediaMetaData GetMetaData(string mediaItemFilePath, MediaClassification mediaType)
        {
            MediaMetaData result = null;

            try
            {
                using (TagLib.File tf = TagLib.File.Create(mediaItemFilePath))
                {
                    tf.Mode = TagLib.File.AccessMode.Read;

                    result = new MediaMetaData
                    {
                        Title = StripNewLines(tf.Tag?.Title),
                        Duration = tf.Properties.Duration
                    };
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, $"Could not get metadata from file: {mediaItemFilePath}");
            }

            return result;
        }

        private string StripNewLines(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }

            return s.Replace("\r\n", " ").Replace("\n", " ");
        }
    }
}
