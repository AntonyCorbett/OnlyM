using System;
using OnlyM.Models;

namespace OnlyM.Services.MetaDataQueue
{
    internal sealed class ItemMetaDataPopulatedEventArgs : EventArgs
    {
        public MediaItem? MediaItem { get; set; }
    }
}
