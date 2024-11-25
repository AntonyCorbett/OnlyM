// Ignore Spelling: ffmpeg

using System;
using System.IO;
using OnlyM.Core.Models;
using OnlyM.Core.Utils;
using Serilog;
using Unosquare.FFME.Common;

namespace OnlyM.Core.Services.Media;

// ReSharper disable once ClassNeverInstantiated.Global
public class MediaMetaDataService : IMediaMetaDataService
{
    public MediaMetaData? GetMetaData(
        string mediaItemFilePath,
        SupportedMediaType mediaType,
        string ffmpegFolder)
    {
        try
        {
            switch (mediaType.Classification)
            {
                case MediaClassification.Video:
                    return GetVideoMetaData(mediaItemFilePath, ffmpegFolder);

                case MediaClassification.Audio:
                case MediaClassification.Image:
                    return GetNonVideoMetaData(mediaItemFilePath);

                case MediaClassification.Web:
                    return GetWebPageMetaData(mediaItemFilePath);

                case MediaClassification.Slideshow:
                    return null;
            }
        }
        catch (VideoFileInUseException)
        {
            Log.Logger.Information($"Waiting for file to become available: {mediaItemFilePath}");
        }
        catch (IOException)
        {
            Log.Logger.Error($"Could not get metadata from file: {mediaItemFilePath} (in use)");
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, $"Could not get metadata from file: {mediaItemFilePath}");
        }

        return null;
    }

    private static string? StripNewLines(string? s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return s;
        }

        return s.Replace("\r\n", " ").Replace('\n', ' ');
    }

    private static MediaMetaData GetVideoMetaData(string mediaItemFilePath, string ffmpegFolder)
    {
        Unosquare.FFME.Library.FFmpegDirectory = ffmpegFolder;
        Unosquare.FFME.Library.LoadFFmpeg();

        try
        {
            var info = Unosquare.FFME.Library.RetrieveMediaInfo(FFmpegUtils.FixUnicodeString(mediaItemFilePath));

            string? title = null;
            info.Metadata?.TryGetValue("title", out title);

            if (string.IsNullOrEmpty(title))
            {
                title = Path.GetFileNameWithoutExtension(mediaItemFilePath);
            }

            return new MediaMetaData
            {
                Title = StripNewLines(title),
                Duration = info.Duration,
                VideoRotation = GetVideoRotation(info)
            };
        }
        catch (MediaContainerException)
        {
            // file is in use...
            throw new VideoFileInUseException();
        }
    }

    private static int GetVideoRotation(MediaInfo info)
    {
        foreach (var stream in info.BestStreams)
        {
            if (stream.Value.Metadata == null)
            {
                continue;
            }

            if (stream.Value.Metadata.TryGetValue("rotate", out var value) &&
                value != null &&
                double.TryParse(value, out var valAsNum))
            {
                return (int)valAsNum;
            }
        }

        return 0;
    }

    private static MediaMetaData GetWebPageMetaData(string mediaItemFilePath) =>
        new()
        {
            Title = Path.GetFileNameWithoutExtension(mediaItemFilePath),
            Duration = TimeSpan.Zero,
        };

    private static MediaMetaData? GetNonVideoMetaData(string mediaItemFilePath)
    {
        if (IsWebPFormat(mediaItemFilePath) || IsSvgFormat(mediaItemFilePath))
        {
            return null;
        }

        using var tf = TagLib.File.Create(mediaItemFilePath);
        tf.Mode = TagLib.File.AccessMode.Read;

        return new MediaMetaData
        {
            Title = StripNewLines(tf.Tag?.Title),
            Duration = tf.Properties?.Duration ?? TimeSpan.Zero,
        };
    }

    private static bool IsWebPFormat(string mediaItemFilePath) =>
        !string.IsNullOrWhiteSpace(mediaItemFilePath) &&
        mediaItemFilePath.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);

    private static bool IsSvgFormat(string mediaItemFilePath) =>
        !string.IsNullOrWhiteSpace(mediaItemFilePath) &&
        mediaItemFilePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
}
