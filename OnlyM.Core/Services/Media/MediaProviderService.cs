﻿namespace OnlyM.Core.Services.Media
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Models;
    using Options;

    // ReSharper disable once ClassNeverInstantiated.Global
    public class MediaProviderService : IMediaProviderService
    {
        private readonly SupportedMediaType[] _supportedMediaTypes =
        {
            new SupportedMediaType { Name = "MP4 Video", Classification = MediaClassification.Video, FileExtension = ".mp4" },
            new SupportedMediaType { Name = "M4V Video", Classification = MediaClassification.Video, FileExtension = ".m4v" },
            new SupportedMediaType { Name = "MKV Video", Classification = MediaClassification.Video, FileExtension = ".mkv" },
            new SupportedMediaType { Name = "WMV Video", Classification = MediaClassification.Video, FileExtension = ".wmv" },
            new SupportedMediaType { Name = "MPEG Video", Classification = MediaClassification.Video, FileExtension = ".mpeg" },
            new SupportedMediaType { Name = "AVI Video", Classification = MediaClassification.Video, FileExtension = ".avi" },
            new SupportedMediaType { Name = "FLV Video", Classification = MediaClassification.Video, FileExtension = ".flv" },
            new SupportedMediaType { Name = "MOV Video", Classification = MediaClassification.Video, FileExtension = ".mov" },

            new SupportedMediaType { Name = "JPG Image", Classification = MediaClassification.Image, FileExtension = ".jpg" },
            new SupportedMediaType { Name = "JPEG Image", Classification = MediaClassification.Image, FileExtension = ".jpeg" },
            new SupportedMediaType { Name = "JPE Image", Classification = MediaClassification.Image, FileExtension = ".jpe" },
            new SupportedMediaType { Name = "PNG Image", Classification = MediaClassification.Image, FileExtension = ".png" },
            new SupportedMediaType { Name = "BMP Image", Classification = MediaClassification.Image, FileExtension = ".bmp" },
            new SupportedMediaType { Name = "GIF Image", Classification = MediaClassification.Image, FileExtension = ".gif" },
            new SupportedMediaType { Name = "ICO Image", Classification = MediaClassification.Image, FileExtension = ".ico" },
            new SupportedMediaType { Name = "TIFF Image", Classification = MediaClassification.Image, FileExtension = ".tiff" },
            
            new SupportedMediaType { Name = "MP3 Audio", Classification = MediaClassification.Audio, FileExtension = ".mp3" },
            new SupportedMediaType { Name = "WMA Audio", Classification = MediaClassification.Audio, FileExtension = ".wma" },
            new SupportedMediaType { Name = "WMP Image", Classification = MediaClassification.Audio, FileExtension = ".wmp" },

            new SupportedMediaType { Name = "OnlyM Slideshow", Classification = MediaClassification.Slideshow, FileExtension = ".omslide" },
        };

        private readonly HashSet<string> _supportedFileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly IOptionsService _optionsService;

        public MediaProviderService(IOptionsService optionsService)
        {
            _optionsService = optionsService;

            foreach (var item in _supportedMediaTypes)
            {
                _supportedFileExtensions.Add(item.FileExtension);
            }
        }

        public MediaFolders GetMediaFolders(DateTime theDate)
        {
            var result = new MediaFolders();

            var mediaFolder = _optionsService.Options.MediaFolder;
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

            var folders = GetMediaFolders(_optionsService.Options.OperatingDate);

            result.AddRange(GetMediaFilesInFolder(folders.MediaFolder));
            result.AddRange(GetMediaFilesInFolder(folders.DatedSubFolder));

            return result;
        }

        public IReadOnlyCollection<SupportedMediaType> GetSupportedMediaTypes()
        {
            return _supportedMediaTypes;
        }

        public bool IsFileExtensionSupported(string extension)
        {
            if (string.IsNullOrEmpty(extension))
            {
                return false;
            }

            return _supportedFileExtensions.Contains(extension);
        }

        public SupportedMediaType GetSupportedMediaType(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return null;
            }

            var extension = Path.GetExtension(fileName);
            return _supportedMediaTypes.SingleOrDefault(x =>
                x.FileExtension.Equals(extension, StringComparison.OrdinalIgnoreCase));
        }

        private IReadOnlyCollection<MediaFile> GetMediaFilesInFolder(string folder)
        {
            var result = new List<MediaFile>();

            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return result;
            }

            var files = Directory.GetFiles(folder);
            foreach (var file in files)
            {
                var mediaType = GetSupportedMediaType(file);
                var lastChanged = File.GetLastWriteTimeUtc(file);

                if (mediaType != null)
                {
                    result.Add(new MediaFile
                    {
                        FullPath = file,
                        MediaType = mediaType,
                        LastChanged = lastChanged.Ticks
                    });
                }
            }

            return result;
        }
    }
}
