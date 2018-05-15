namespace OnlyM.Core.Services.Media
{
    using System;

    public interface IFolderWatcherService
    {
        bool IsEnabled { get; set; }

        event EventHandler ChangesFoundEvent;
    }
}
