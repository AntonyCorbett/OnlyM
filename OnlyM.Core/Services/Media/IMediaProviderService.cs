namespace OnlyM.Core.Services.Media
{
    using System;
    using System.Collections.Generic;
    using OnlyM.Core.Models;

    public interface IMediaProviderService
    {
        IReadOnlyCollection<MediaFile> GetMediaFiles();

        bool IsFileExtensionSupported(string extension);

        IReadOnlyCollection<SupportedMediaType> GetSupportedMediaTypes();

        SupportedMediaType GetSupportedMediaType(string fileName);

        MediaFolders GetMediaFolders(DateTime theDate);
    }
}
