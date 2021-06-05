namespace OnlyM.Services.MediaChanging
{
    using System;

    internal interface IMediaStatusChangingService
    {
        void AddChangingItem(Guid mediaItemId);

        void RemoveChangingItem(Guid mediaItemId);

        bool IsMediaStatusChanging();
    }
}
