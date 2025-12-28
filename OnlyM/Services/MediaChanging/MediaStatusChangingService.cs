using System;
using System.Collections.Generic;
using System.Threading;

namespace OnlyM.Services.MediaChanging;

internal sealed class MediaStatusChangingService : IMediaStatusChangingService
{
    private readonly HashSet<Guid> _changingMediaItems = [];
    private readonly Lock _locker = new();

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
