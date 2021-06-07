using OnlyM.Core.Models;

namespace OnlyM.Core.Services.Media
{
    public interface IMediaMetaDataService
    {
        MediaMetaData GetMetaData(
            string mediaItemFilePath, 
            SupportedMediaType mediaType,
            string ffmpegFolder);
    }
}
