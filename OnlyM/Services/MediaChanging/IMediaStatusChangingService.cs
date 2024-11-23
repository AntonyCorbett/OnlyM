using System;

namespace OnlyM.Services.MediaChanging;

internal interface IMediaStatusChangingService
{
    void AddChangingItem(Guid mediaItemId);

    void RemoveChangingItem(Guid mediaItemId);

    bool IsMediaStatusChanging();
}
