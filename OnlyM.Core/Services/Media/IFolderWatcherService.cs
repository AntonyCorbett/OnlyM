using System;

namespace OnlyM.Core.Services.Media;

public interface IFolderWatcherService
{
    event EventHandler ChangesFoundEvent;

    bool IsEnabled { get; set; }
}
