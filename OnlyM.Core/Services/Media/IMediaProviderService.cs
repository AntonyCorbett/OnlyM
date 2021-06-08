using System;
using System.Collections.Generic;
using OnlyM.Core.Models;

namespace OnlyM.Core.Services.Media
{
    public interface IMediaProviderService
    {
        IReadOnlyCollection<MediaFile> GetMediaFiles();

        bool IsFileExtensionSupported(string extension);

        IReadOnlyCollection<SupportedMediaType> GetSupportedMediaTypes();

        SupportedMediaType? GetSupportedMediaType(string? fileName);

        MediaFolders GetMediaFolders(DateTime theDate);
    }
}
