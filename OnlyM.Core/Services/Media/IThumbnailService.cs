namespace OnlyM.Core.Services.Media
{
    using System;
    using Models;

    public interface IThumbnailService
    {
        byte[] GetThumbnail(
            string originalPath, 
            string ffmpegFolder,
            MediaClassification mediaClassification, 
            long originalLastChanged, 
            out bool foundInCache);

        void ClearThumbCache();
        
        event EventHandler ThumbnailsPurgedEvent;
    }
}
