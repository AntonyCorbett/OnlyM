namespace OnlyM.Services.MediaChanging
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Core.Models;

    internal class ActiveMediaItemsService : IActiveMediaItemsService
    {
        private readonly Dictionary<Guid, MediaClassification> _currentMedia = new Dictionary<Guid, MediaClassification>();

        public void Add(Guid mediaItemId, MediaClassification classification)
        {
            _currentMedia[mediaItemId] = classification;
        }

        public void Remove(Guid mediaItemId)
        {
            _currentMedia.Remove(mediaItemId);
        }

        public bool Exists(Guid mediaItemId)
        {
            return _currentMedia.ContainsKey(mediaItemId);
        }

        public bool Any(params MediaClassification[] classifications)
        {
            return _currentMedia.Any(x => classifications.Contains(x.Value));
        }

        public bool Any()
        {
            return _currentMedia.Any();
        }

        public IReadOnlyCollection<Guid> GetMediaItemIds()
        {
            return _currentMedia.Keys;
        }
    }
}
