namespace OnlyM.Core.Services.Media
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

            new SupportedMediaType { Name = "JPG Image", Classification = MediaClassification.Image, FileExtension = ".jpg" },
            new SupportedMediaType { Name = "JPEG Image", Classification = MediaClassification.Image, FileExtension = ".jpeg" },
            new SupportedMediaType { Name = "PNG Image", Classification = MediaClassification.Image, FileExtension = ".png" },

            new SupportedMediaType { Name = "MP3 Audio", Classification = MediaClassification.Audio, FileExtension = ".mp3" },
            new SupportedMediaType { Name = "WMA Audio", Classification = MediaClassification.Audio, FileExtension = ".wma" }
        };

        private readonly List<string> _supportedFileExtensions = new List<string>();
        private readonly IOptionsService _optionsService;

        public MediaProviderService(IOptionsService optionsService)
        {
            _optionsService = optionsService;
            _supportedFileExtensions.AddRange(_supportedMediaTypes.Select(x => x.FileExtension));
        }

        public IReadOnlyCollection<MediaFile> GetMediaFiles()
        {
            var result = new List<MediaFile>();

            var mediaFolder = _optionsService.Options.MediaFolder;

            if (!Directory.Exists(mediaFolder))
            {
                return result;
            }

            var files = Directory.GetFiles(mediaFolder);
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

            return _supportedFileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
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
    }
}
