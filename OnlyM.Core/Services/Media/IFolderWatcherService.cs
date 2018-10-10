namespace OnlyM.Core.Services.Media
{
    using System;

    public interface IFolderWatcherService
    {
        event EventHandler ChangesFoundEvent;

        bool IsEnabled { get; set; }
    }
}
