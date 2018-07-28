namespace OnlyM.Services.MediaChanging
{
    using System;
    using System.Collections.Generic;
    using Core.Models;

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
