namespace OnlyM.Services.HiddenMediaItems
{
    using System;
    using System.Collections.Generic;
    using OnlyM.Models;

    internal interface IHiddenMediaItemsService
    {
        event EventHandler HiddenItemsChangedEvent;

        event EventHandler UnhideAllEvent;

        void Init(IEnumerable<MediaItem> items);

        void Add(string path);

        void Remove(string path);

        bool SomeHiddenMediaItems();

        void UnhideAllMediaItems();
    }
}
