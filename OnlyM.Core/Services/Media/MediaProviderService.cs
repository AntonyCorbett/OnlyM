using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OnlyM.Core.Models;
using OnlyM.Core.Services.Options;
using OnlyM.Slides;
using Serilog;

namespace OnlyM.Core.Services.Media;

// ReSharper disable once ClassNeverInstantiated.Global
public class MediaProviderService : IMediaProviderService
{
    private readonly SupportedMediaType[] _supportedMediaTypes =
    [
        new() { Name = "MP4 Video", Classification = MediaClassification.Video, FileExtension = ".mp4" },
        new() { Name = "M4V Video", Classification = MediaClassification.Video, FileExtension = ".m4v" },
        new() { Name = "MKV Video", Classification = MediaClassification.Video, FileExtension = ".mkv" },
        new() { Name = "WMV Video", Classification = MediaClassification.Video, FileExtension = ".wmv" },
        new() { Name = "MPEG Video", Classification = MediaClassification.Video, FileExtension = ".mpeg" },
        new() { Name = "AVI Video", Classification = MediaClassification.Video, FileExtension = ".avi" },
        new() { Name = "FLV Video", Classification = MediaClassification.Video, FileExtension = ".flv" },
        new() { Name = "MOV Video", Classification = MediaClassification.Video, FileExtension = ".mov" },
        new() { Name = "WEBM Video", Classification = MediaClassification.Video, FileExtension = ".webm" },

        new() { Name = "JPG Image", Classification = MediaClassification.Image, FileExtension = ".jpg" },
        new() { Name = "JPEG Image", Classification = MediaClassification.Image, FileExtension = ".jpeg" },
        new() { Name = "JPE Image", Classification = MediaClassification.Image, FileExtension = ".jpe" },
        new() { Name = "PNG Image", Classification = MediaClassification.Image, FileExtension = ".png" },
        new() { Name = "BMP Image", Classification = MediaClassification.Image, FileExtension = ".bmp" },
        new() { Name = "GIF Image", Classification = MediaClassification.Image, FileExtension = ".gif" },
        new() { Name = "ICO Image", Classification = MediaClassification.Image, FileExtension = ".ico" },
        new() { Name = "TIFF Image", Classification = MediaClassification.Image, FileExtension = ".tiff" },
        new() { Name = "JFIFF Image", Classification = MediaClassification.Image, FileExtension = ".jfif" },
        new() { Name = "WEBP Image", Classification = MediaClassification.Image, FileExtension = ".webp" },
        new() { Name = "SVG Image", Classification = MediaClassification.Image, FileExtension = ".svg" },
        // Added HEIF/HEIC support:
        new() { Name = "HEIC Image", Classification = MediaClassification.Image, FileExtension = ".heic" },
        new() { Name = "HEIF Image", Classification = MediaClassification.Image, FileExtension = ".heif" },

        new() { Name = "MP3 Audio", Classification = MediaClassification.Audio, FileExtension = ".mp3" },
        new() { Name = "M4A Audio", Classification = MediaClassification.Audio, FileExtension = ".m4a" },
        new() { Name = "WMA Audio", Classification = MediaClassification.Audio, FileExtension = ".wma" },
        new() { Name = "WMP Audio", Classification = MediaClassification.Audio, FileExtension = ".wmp" },
        new() { Name = "WAV Audio", Classification = MediaClassification.Audio, FileExtension = ".wav" },
        new() { Name = "IAF Audio", Classification = MediaClassification.Audio, FileExtension = ".aif" },
        new() { Name = "IAFF Audio", Classification = MediaClassification.Audio, FileExtension = ".aiff" },

        new() { Name = "OnlyM Slideshow", Classification = MediaClassification.Slideshow, FileExtension = SlideFile.FileExtension },

        new() { Name = "Web Page", Classification = MediaClassification.Web, FileExtension = ".url" },
        new() { Name = "PDF file", Classification = MediaClassification.Web, FileExtension = ".pdf" }
    ];

    private readonly HashSet<string> _supportedFileExtensions = new(StringComparer.OrdinalIgnoreCase);
    private readonly IOptionsService _optionsService;

    public MediaProviderService(IOptionsService optionsService)
    {
        _optionsService = optionsService;

        foreach (var item in _supportedMediaTypes)
        {
            if (item.FileExtension != null)
            {
                _supportedFileExtensions.Add(item.FileExtension);
            }
        }
    }

    public MediaFolders GetMediaFolders(DateTime theDate)
    {
        var result = new MediaFolders();

        var mediaFolder = _optionsService.MediaFolder;
        result.MediaFolder = mediaFolder;

        var subFolder = DatedSubFolders.GetDatedSubFolder(mediaFolder, theDate);
        if (subFolder != null)
        {
            result.DatedSubFolder = subFolder;
        }

        return result;
    }

    public IReadOnlyCollection<MediaFile> GetMediaFiles()
    {
        var result = new List<MediaFile>();

        var folders = GetMediaFolders(_optionsService.OperatingDate);

        result.AddRange(GetMediaFilesInFolder(folders.MediaFolder));
        result.AddRange(GetMediaFilesInFolder(folders.DatedSubFolder));

        return result;
    }

    public IReadOnlyCollection<SupportedMediaType> GetSupportedMediaTypes() => _supportedMediaTypes;

    public bool IsFileExtensionSupported(string extension)
    {
        return !string.IsNullOrEmpty(extension) && _supportedFileExtensions.Contains(extension);
    }

    public SupportedMediaType? GetSupportedMediaType(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return null;
        }

        var extension = Path.GetExtension(fileName);

        var result = _supportedMediaTypes.SingleOrDefault(x =>
            x.FileExtension != null &&
            x.FileExtension.Equals(extension, StringComparison.OrdinalIgnoreCase));

        if (result != null &&
            result.Classification == MediaClassification.Web &&
            IsPdf(fileName) &&
            fileName.Contains('#'))
        {
            // the CefBrowser doesn't support files with '#' character in path!
            // work-around this by logging and saying unsupported...
            Log.Logger.Warning("'{FileName}' - web files with embedded # character not supported - rename the file!", fileName);
            return null;
        }

        return result;
    }

    private static bool IsPdf(string fileName)
    {
        return !string.IsNullOrEmpty(fileName) &&
               Path.GetExtension(fileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase);
    }

    private List<MediaFile> GetMediaFilesInFolder(string? folder)
    {
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            return [];
        }

        var files = Directory.GetFiles(folder);

        var result = new List<MediaFile>(files.Length);

        foreach (var file in files)
        {
            var mediaType = GetSupportedMediaType(file);

            if (mediaType == null)
            {
                continue;
            }

            var lastChanged = File.GetLastWriteTimeUtc(file);

            result.Add(new MediaFile
            {
                FullPath = file,
                MediaType = mediaType,
                LastChanged = lastChanged.Ticks,
            });
        }

        return result;
    }
}
