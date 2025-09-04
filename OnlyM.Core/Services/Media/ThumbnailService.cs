using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.WindowsAPICodePack.Shell;
using OnlyM.Core.Models;
using OnlyM.Core.Services.Database;
using OnlyM.Core.Services.Options;
using OnlyM.Core.Services.WebShortcuts;
using OnlyM.Core.Utils;
using OnlyM.CoreSys;
using OnlyM.Slides;
using Serilog;
using Serilog.Events;

namespace OnlyM.Core.Services.Media;

public sealed class ThumbnailService : IThumbnailService
{
    private const int MaxPixelDimension = 320;

    private readonly IDatabaseService _databaseService;
    private readonly IOptionsService _optionsService;

    // Helper centralizes conversion + disposal.
    private static byte[]? ToBytesAndDispose(Bitmap? bmp, ImageFormat? format = null)
    {
        if (bmp == null)
        {
            return null;
        }

        try
        {
            using (bmp)
            {
                using var ms = new MemoryStream();
                // Default to PNG for lossless UI assets (smaller & preserves transparency if any).
                bmp.Save(ms, format ?? ImageFormat.Png);
                return ms.ToArray();
            }
        }
        catch (Exception ex)
        {
            Log.Logger.Warning(ex, "Could not convert resource bitmap");
            return null;
        }
    }

    private readonly Lazy<byte[]?> _standardAudioThumbnail =
        new(() => ToBytesAndDispose(Properties.Resources.Audio));

    private readonly Lazy<byte[]?> _standardPdfThumbnail =
        new(() => ToBytesAndDispose(Properties.Resources.PDF));

    private readonly Lazy<byte[]?> _standardWebThumbnail =
        new(() => ToBytesAndDispose(Properties.Resources.Web));

    private readonly Lazy<byte[]?> _standardUnknownThumbnail =
        new(() => ToBytesAndDispose(Properties.Resources.Unknown));

    public ThumbnailService(IDatabaseService databaseService, IOptionsService optionsService)
    {
        _databaseService = databaseService;
        _optionsService = optionsService;
    }

    public event EventHandler? ThumbnailsPurgedEvent;

    public byte[]? GetThumbnail(
        string originalPath,
        string ffmpegFolder,
        MediaClassification mediaClassification,
        long originalLastChanged,
        out bool foundInCache)
    {
        if (Log.Logger.IsEnabled(LogEventLevel.Debug))
        {
            Log.Logger.Debug("Getting thumbnail: {Path}", originalPath);
        }

        var result = _databaseService.GetThumbnailFromCache(originalPath, originalLastChanged);
        if (result != null)
        {
            if (Log.Logger.IsEnabled(LogEventLevel.Verbose))
            {
                Log.Logger.Verbose("Found thumbnail in cache");
            }

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
            Log.Logger.Error(ex, "Could not get a thumbnail for {Path}", originalPath);
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

    private byte[]? GenerateThumbnail(
        string originalPath,
        string ffmpegFolder,
        MediaClassification mediaClassification)
    {
        if (Log.Logger.IsEnabled(LogEventLevel.Debug))
        {
            Log.Logger.Debug("Generating thumbnail");
        }

        var tempThumbnailFolder = Path.Combine(FileUtils.GetUsersTempFolder(), "OnlyM", "TempThumbs");
        FileUtils.CreateDirectory(tempThumbnailFolder);

        switch (mediaClassification)
        {
            case MediaClassification.Image:
                return GraphicsUtils.CreateThumbnailOfImage(originalPath, MaxPixelDimension);

            case MediaClassification.Video:
                {
                    var tempFile = GraphicsUtils.CreateThumbnailForVideo(
                        originalPath,
                        ffmpegFolder,
                        tempThumbnailFolder,
                        _optionsService.EmbeddedThumbnails);

                    if (string.IsNullOrEmpty(tempFile) || !File.Exists(tempFile))
                    {
                        return null;
                    }

                    try
                    {
                        var bytes = File.ReadAllBytes(tempFile);
                        TryDeleteTempFile(tempFile);
                        return bytes;
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Warning(ex, "Could not read/delete temp video thumbnail {File}", tempFile);
                        TryDeleteTempFile(tempFile);
                        return null;
                    }
                }

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

    private static void TryDeleteTempFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            Log.Logger.Debug(ex, "Could not delete temp thumbnail file {File}", path);
        }
    }

    private byte[]? GetWebThumbnail(string originalPath)
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
        return bytes != null
            ? CreateFramedSmallIcon(bytes)
            : _standardWebThumbnail.Value;
    }

    private byte[]? GetPdfThumbnail(string originalPath)
    {
        try
        {
            using var o = ShellObject.FromParsingName(originalPath);
            if (o?.Thumbnail?.BitmapSource == null)
            {
                return _standardPdfThumbnail.Value;
            }

            var encoder = new JpegBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(o.Thumbnail.BitmapSource));

            using var stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }
        catch (Exception ex)
        {
            Log.Logger.Warning(ex, "Could not get PDF thumbnail {Path}", originalPath);
            return _standardPdfThumbnail.Value;
        }
    }

    private static byte[]? CreateFramedSmallIcon(byte[] bytes)
    {
        const int pixelSize = 100;

        var image = GraphicsUtils.ByteArrayToImage(bytes);
        if (image == null)
        {
            return null;
        }

        if (!(Math.Max(image.Height, image.Width) < pixelSize))
        {
            return bytes;
        }

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

        using var stream = new MemoryStream();
        encoder.Save(stream);
        bytes = stream.ToArray();

        return bytes;
    }

    private byte[]? GetSlideshowThumbnail(string originalPath)
    {
        var file = new SlideFile(originalPath);
        if (file.SlideCount == 0)
        {
            return _standardUnknownThumbnail.Value;
        }

        var slide = file.GetSlide(0);
        return slide.Image == null
            ? _standardUnknownThumbnail.Value
            : GraphicsUtils.CreateThumbnailOfImage(slide.Image, MaxPixelDimension);
    }

    private void OnThumbnailsPurgedEvent() =>
        ThumbnailsPurgedEvent?.Invoke(this, EventArgs.Empty);
}
