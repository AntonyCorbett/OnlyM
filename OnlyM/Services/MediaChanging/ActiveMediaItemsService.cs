using System;
using System.Collections.Generic;
using System.Linq;
using OnlyM.Core.Models;

namespace OnlyM.Services.MediaChanging
{
    internal class ActiveMediaItemsService : IActiveMediaItemsService
    {
        private readonly Dictionary<Guid, MediaClassification> _currentMedia = new();

        public void Add(Guid mediaItemId, MediaClassification classification) => _currentMedia[mediaItemId] = classification;
        
        public void Remove(Guid mediaItemId) => _currentMedia.Remove(mediaItemId);
        
        public bool Exists(Guid mediaItemId) => _currentMedia.ContainsKey(mediaItemId);
        
        public bool Any(params MediaClassification[] classifications) => _currentMedia.Any(x => classifications.Contains(x.Value));
        
        public bool Any() => _currentMedia.Count > 0;
        
        public IReadOnlyCollection<Guid> GetMediaItemIds() => _currentMedia.Keys;
    }
}
