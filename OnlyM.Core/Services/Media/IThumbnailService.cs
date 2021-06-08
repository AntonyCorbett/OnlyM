using System;
using OnlyM.Core.Models;

namespace OnlyM.Core.Services.Media
{
    public interface IThumbnailService
    {
        event EventHandler ThumbnailsPurgedEvent;

        byte[]? GetThumbnail(
            string originalPath, 
            string ffmpegFolder,
            MediaClassification mediaClassification, 
            long originalLastChanged, 
            out bool foundInCache);

        void ClearThumbCache();
    }
}
