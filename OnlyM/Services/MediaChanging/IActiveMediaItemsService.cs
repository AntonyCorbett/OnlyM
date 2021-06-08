using System;
using System.Collections.Generic;
using OnlyM.Core.Models;

namespace OnlyM.Services.MediaChanging
{
    public interface IActiveMediaItemsService
    {
        void Add(Guid mediaItemId, MediaClassification classification);

        void Remove(Guid mediaItemId);

        bool Exists(Guid mediaItemId);

        bool Any(params MediaClassification[] classifications);

        bool Any();

        IReadOnlyCollection<Guid> GetMediaItemIds();
    }
}
