namespace OnlyM.Core.Services.Media
{
    using System;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using Database;
    using Models;
    using Options;
    using Serilog;
    using Utils;

    public sealed class ThumbnailService : IThumbnailService
    {
        private const int MaxPixelDimension = 180;
        private readonly IDatabaseService _databaseService;
        private readonly IOptionsService _optionsService;

        private readonly Lazy<byte[]> _standardAudioThumbnail = new Lazy<byte[]>(() =>
        {
            var bmp = Properties.Resources.Audio;
            var converter = new ImageConverter();
            return (byte[])converter.ConvertTo(bmp, typeof(byte[]));
        });

        private readonly Lazy<byte[]> _standardUnknownThumbnail = new Lazy<byte[]>(() =>
        {
            var bmp = Properties.Resources.Unknown;
            var converter = new ImageConverter();
            return (byte[])converter.ConvertTo(bmp, typeof(byte[]));
        });

        public event EventHandler ThumbnailsPurgedEvent;

        public ThumbnailService(IDatabaseService databaseService, IOptionsService optionsService)
        {
            _databaseService = databaseService;
            _optionsService = optionsService;
        }

        public byte[] GetThumbnail(
            string originalPath, 
            string ffmpegFolder,
            MediaClassification mediaClassification, 
            long originalLastChanged, 
            out bool foundInCache)
        {
            Log.Logger.Debug($"Getting thumbnail: {originalPath}");
            
            byte[] result = _databaseService.GetThumbnailFromCache(originalPath, originalLastChanged);
            if (result != null)
            {
                Log.Logger.Verbose("Found thumbnail in cache");
                foundInCache = true;
                return result;
            }

            try
            {
                result = GenerateThumbnail(originalPath, ffmpegFolder, mediaClassification);
                if (result != null)
                {
                    _databaseService.AddThumbnailToCache(originalPath, originalLastChanged, result);
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, $"Could not get a thumbnail for {originalPath}");
                result = _standardUnknownThumbnail.Value;
            }
            
            foundInCache = false;
            return result;
        }

        public void ClearThumbCache()
        {
            _databaseService.ClearThumbCache();
            OnThumbnailsPurgedEvent();
        }

        private byte[] GenerateThumbnail(
            string originalPath,
            string ffmpegFolder,
            MediaClassification mediaClassification)
        {
            Log.Logger.Debug("Generating thumbnail");
            
            switch (mediaClassification)
            {
                case MediaClassification.Image:
                    return GraphicsUtils.CreateThumbnailOfImage(originalPath, MaxPixelDimension, ImageFormat.Jpeg);

                case MediaClassification.Video:
                    var tempFile = GraphicsUtils.CreateThumbnailForVideo(
                        originalPath, 
                        ffmpegFolder, 
                        _optionsService.Options.EmbeddedThumbnails);

                    if (string.IsNullOrEmpty(tempFile))
                    {
                        return null;
                    }

                    return File.ReadAllBytes(tempFile);

                case MediaClassification.Audio:
                    return _standardAudioThumbnail.Value;

                default:
                    return null;
            }
        }

        private void OnThumbnailsPurgedEvent()
        {
            ThumbnailsPurgedEvent?.Invoke(this, EventArgs.Empty);
        }
    }
}
