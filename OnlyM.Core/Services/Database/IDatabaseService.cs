namespace OnlyM.Core.Services.Database
{
    public interface IDatabaseService
    {
        // thumbnails...
        void ClearThumbCache();

        void AddThumbnailToCache(string originalPath, long originalLastChanged, byte[] thumbnail);

        byte[] GetThumbnailFromCache(string originalPath, long originalLastChanged);

        // browser data...
        void AddBrowserData(string url, double zoomLevel);

        BrowserData GetBrowserData(string url);

        // media file start offset data...
        void AddMediaStartOffsetData(string fileName, string startOffsets, int lengthSeconds);

        MediaStartOffsetData GetMediaStartOffsetData(string fileName);
    }
}
