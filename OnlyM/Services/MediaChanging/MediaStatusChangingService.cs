using System;
using System.Collections.Generic;

namespace OnlyM.Services.MediaChanging
{
    internal sealed class MediaStatusChangingService : IMediaStatusChangingService
    {
        private readonly HashSet<Guid> _changingMediaItems = new();
        private readonly object _locker = new();

        public void AddChangingItem(Guid mediaItemId)
        {
            lock (_locker)
            {
                _changingMediaItems.Add(mediaItemId);
            }
        }

        public void RemoveChangingItem(Guid mediaItemId)
        {
            lock (_locker)
            {
                _changingMediaItems.Remove(mediaItemId);
            }
        }

        public bool IsMediaStatusChanging()
        {
            lock (_locker)
            {
                return _changingMediaItems.Count > 0;
            }
        }
    }
}
