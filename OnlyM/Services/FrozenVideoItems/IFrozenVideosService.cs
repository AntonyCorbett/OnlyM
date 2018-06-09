namespace OnlyM.Services.FrozenVideoItems
{
    using System.Collections.Generic;
    using Models;

    internal interface IFrozenVideosService
    {
        void Init(IEnumerable<MediaItem> items);

        void Add(string path);

        void Remove(string path);
    }
}
