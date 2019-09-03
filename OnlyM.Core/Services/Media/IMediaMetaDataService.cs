namespace OnlyM.Core.Services.Media
{
    using OnlyM.Core.Models;

    public interface IMediaMetaDataService
    {
        MediaMetaData GetMetaData(
            string mediaItemFilePath, 
            SupportedMediaType mediaType,
            string ffmpegFolder);
    }
}
