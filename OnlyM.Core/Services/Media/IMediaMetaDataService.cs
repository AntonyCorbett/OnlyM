namespace OnlyM.Core.Services.Media
{
    using Models;

    public interface IMediaMetaDataService
    {
        MediaMetaData GetMetaData(
            string mediaItemFilePath, 
            SupportedMediaType mediaType,
            string ffmpegFolder);
    }
}
