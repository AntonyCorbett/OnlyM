namespace OnlyM.Core.Services.Media
{
    using System.Collections.Generic;
    using Models;

    public interface IMediaProviderService
    {
        IReadOnlyCollection<MediaFile> GetMediaFiles();

        bool IsFileExtensionSupported(string extension);

        IReadOnlyCollection<SupportedMediaType> GetSupportedMediaTypes();

        SupportedMediaType GetSupportedMediaType(string fileName);
    }
}
