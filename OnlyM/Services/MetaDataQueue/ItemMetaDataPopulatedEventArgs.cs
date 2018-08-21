namespace OnlyM.Services.MetaDataQueue
{
    using System;
    using Models;

    internal class ItemMetaDataPopulatedEventArgs : EventArgs
    {
        public MediaItem MediaItem { get; set; }
    }
}
