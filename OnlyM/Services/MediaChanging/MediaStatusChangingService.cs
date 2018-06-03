namespace OnlyM.Services.MediaChanging
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal class MediaStatusChangingService : IMediaStatusChangingService
    {
        private readonly HashSet<Guid> _changingMediaItems = new HashSet<Guid>();

        public void AddChangingItem(Guid mediaItemId)
        {
            _changingMediaItems.Add(mediaItemId);
        }

        public void RemoveChangingItem(Guid mediaItemId)
        {
            _changingMediaItems.Remove(mediaItemId);
        }

        public bool IsMediaStatusChanging()
        {
            return _changingMediaItems.Any();
        }
    }
}
