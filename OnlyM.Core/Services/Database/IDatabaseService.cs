namespace OnlyM.Core.Services.Database
{
    public interface IDatabaseService
    {
        void ClearThumbCache();

        void AddThumbnailToCache(string originalPath, long originalLastChanged, byte[] thumbnail);

        byte[] GetThumbnailFromCache(string originalPath, long originalLastChanged);

        void AddBrowserData(string url, double zoomLevel);

        BrowserData GetBrowserData(string url);
    }
}
