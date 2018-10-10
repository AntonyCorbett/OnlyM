namespace OnlyM.Core.Services.Media
{
    using System;
    using Models;

    public interface IThumbnailService
    {
        event EventHandler ThumbnailsPurgedEvent;

        byte[] GetThumbnail(
            string originalPath, 
            string ffmpegFolder,
            MediaClassification mediaClassification, 
            long originalLastChanged, 
            out bool foundInCache);

        void ClearThumbCache();
    }
}
