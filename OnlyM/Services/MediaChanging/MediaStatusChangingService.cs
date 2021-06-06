namespace OnlyM.Services.MediaChanging
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal class MediaStatusChangingService : IMediaStatusChangingService
    {
        private readonly HashSet<Guid> _changingMediaItems = new HashSet<Guid>();
        private readonly object _locker = new object();

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
