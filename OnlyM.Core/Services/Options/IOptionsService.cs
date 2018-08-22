namespace OnlyM.Core.Services.Options
{
    using System;
    using Models;

    public interface IOptionsService
    {
        Options Options { get; }

        bool IsMediaMonitorSpecified { get; }

        void Save();

        event EventHandler MediaFolderChangedEvent;

        event EventHandler AutoRotateChangedEvent;

        event EventHandler ImageFadeTypeChangedEvent;

        event EventHandler ImageFadeSpeedChangedEvent;

        event EventHandler AlwaysOnTopChangedEvent;

        event EventHandler<MonitorChangedEventArgs> MediaMonitorChangedEvent;

        event EventHandler RenderingMethodChangedEvent;

        event EventHandler PermanentBackdropChangedEvent;

        event EventHandler AllowVideoPauseChangedEvent;

        event EventHandler AllowVideoPositionSeekingChangedEvent;

        event EventHandler ShowSubtitlesChangedEvent;

        event EventHandler UseInternalMediaTitlesChangedEvent;

        event EventHandler IncludeBlankScreenItemChangedEvent;

        event EventHandler VideoScreenPositionChangedEvent;

        event EventHandler ImageScreenPositionChangedEvent;

        event EventHandler ShowMediaItemCommandPanelChangedEvent;

        event EventHandler OperatingDateChangedEvent;

        event EventHandler MaxItemCountChangedEvent;

        event EventHandler ShowFreezeCommandChangedEvent;
    }
}
