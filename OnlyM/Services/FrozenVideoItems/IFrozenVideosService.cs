using System.Collections.Generic;
using OnlyM.Models;

namespace OnlyM.Services.FrozenVideoItems
{
    internal interface IFrozenVideosService
    {
        void Init(IEnumerable<MediaItem> items);

        void Add(string path);

        void Remove(string path);
    }
}
