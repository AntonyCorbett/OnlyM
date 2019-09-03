namespace OnlyM.Services.MetaDataQueue
{
    using System;
    using OnlyM.Models;

    internal class ItemMetaDataPopulatedEventArgs : EventArgs
    {
        public MediaItem MediaItem { get; set; }
    }
}
