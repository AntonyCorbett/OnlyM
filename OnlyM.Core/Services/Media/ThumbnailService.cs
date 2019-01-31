namespace OnlyM.Core.Services.Media
{
    using System;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Database;
    using Microsoft.WindowsAPICodePack.Shell;
    using Models;
    using OnlyM.Core.Services.WebShortcuts;
    using OnlyM.CoreSys;
    using OnlyM.Slides;
    using Options;
    using Serilog;
    using Utils;

    public sealed class ThumbnailService : IThumbnailService
    {
        // note that MaxPixelDimension should match the 
        // image tooltip width in OperatorPage.xaml
        private const int MaxPixelDimension = 320;

        private readonly IDatabaseService _databaseService;
        private readonly IOptionsService _optionsService;

        private readonly Lazy<byte[]> _standardAudioThumbnail = new Lazy<byte[]>(() =>
        {
            var bmp = Properties.Resources.Audio;
            var converter = new ImageConverter();
            return (byte[])converter.ConvertTo(bmp, typeof(byte[]));
        });

        private readonly Lazy<byte[]> _standardPdfThumbnail = new Lazy<byte[]>(() =>
        {
            var bmp = Properties.Resources.PDF;
            var converter = new ImageConverter();
            return (byte[])converter.ConvertTo(bmp, typeof(byte[]));
        });

        private readonly Lazy<byte[]> _standardWebThumbnail = new Lazy<byte[]>(() =>
        {
            var bmp = Properties.Resources.Web;
            var converter = new ImageConverter();
            return (byte[])converter.ConvertTo(bmp, typeof(byte[]));
        });

        private readonly Lazy<byte[]> _standardUnknownThumbnail = new Lazy<byte[]>(() =>
        {
            var bmp = Properties.Resources.Unknown;
            var converter = new ImageConverter();
            return (byte[])converter.ConvertTo(bmp, typeof(byte[]));
        });

        public ThumbnailService(IDatabaseService databaseService, IOptionsService optionsService)
        {
            _databaseService = databaseService;
            _optionsService = optionsService;
        }

        public event EventHandler ThumbnailsPurgedEvent;

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

            var tempThumbnailFolder = Path.Combine(FileUtils.GetUsersTempFolder(), "OnlyM", "TempThumbs");
            FileUtils.CreateDirectory(tempThumbnailFolder);

            switch (mediaClassification)
            {
                case MediaClassification.Image:
                    return GraphicsUtils.CreateThumbnailOfImage(originalPath, MaxPixelDimension, ImageFormat.Jpeg);

                case MediaClassification.Video:
                    var tempFile = GraphicsUtils.CreateThumbnailForVideo(
                        originalPath, 
                        ffmpegFolder,
                        tempThumbnailFolder,
                        _optionsService.EmbeddedThumbnails);

                    if (string.IsNullOrEmpty(tempFile))
                    {
                        return null;
                    }

                    return File.ReadAllBytes(tempFile);

                case MediaClassification.Audio:
                    return _standardAudioThumbnail.Value;

                case MediaClassification.Slideshow:
                    return GetSlideshowThumbnail(originalPath);

                case MediaClassification.Web:
                    return GetWebThumbnail(originalPath);

                default:
                    return null;
            }
        }

        private byte[] GetWebThumbnail(string originalPath)
        {
            if (string.IsNullOrEmpty(originalPath))
            {
                return null;
            }

            if (Path.GetExtension(originalPath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return GetPdfThumbnail(originalPath);
            }

            var helper = new WebShortcutHelper(originalPath);

            var bytes = FaviconHelper.GetIconImage(helper.Uri);
            if (bytes != null)
            {
                bytes = CreateFramedSmallIcon(bytes);
                return bytes;
            }

            return _standardWebThumbnail.Value;
        }

        private byte[] GetPdfThumbnail(string originalPath)
        {
            try
            {
                var o = ShellObject.FromParsingName(originalPath);

                var encoder = new JpegBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(o.Thumbnail.BitmapSource));

                using (var stream = new MemoryStream())
                {
                    encoder.Save(stream);
                    return stream.ToArray();
                }
            }
            catch (Exception)
            {
                return _standardPdfThumbnail.Value;
            }
        }

        private byte[] CreateFramedSmallIcon(byte[] bytes)
        {
            const int pixelSize = 100;

            var image = GraphicsUtils.ByteArrayToImage(bytes);
            if (Math.Max(image.Height, image.Width) < pixelSize)
            {
                var visual = new DrawingVisual();
                using (var drawingContext = visual.RenderOpen())
                {
                    drawingContext.DrawRectangle(
                        System.Windows.Media.Brushes.Black, 
                        null, 
                        new Rect(0, 0, pixelSize, pixelSize));

                    var left = (pixelSize - image.Width) / 2;
                    var top = (pixelSize - image.Height) / 2;
                    
                    drawingContext.DrawImage(image, new Rect(left, top, image.Width, image.Height));
                }
                
                var bitmap = new RenderTargetBitmap(pixelSize, pixelSize, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(visual);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));

                using (var stream = new MemoryStream())
                {
                    encoder.Save(stream);
                    bytes = stream.ToArray();
                }
            }

            return bytes;
        }

        private byte[] GetSlideshowThumbnail(string originalPath)
        {
            var file = new SlideFile(originalPath);
            if (file.SlideCount == 0)
            {
                return _standardUnknownThumbnail.Value;
            }

            var slide = file.GetSlide(0);
            return GraphicsUtils.CreateThumbnailOfImage(slide.Image, MaxPixelDimension);
        }

        private void OnThumbnailsPurgedEvent()
        {
            ThumbnailsPurgedEvent?.Invoke(this, EventArgs.Empty);
        }
    }
}
