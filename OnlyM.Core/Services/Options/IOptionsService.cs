namespace OnlyM.Core.Services.Options
{
    using System;
    using System.Collections.Generic;
    using Models;
    using Serilog.Events;

    public interface IOptionsService
    {
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

        event EventHandler AllowMirrorChangedEvent;
        
        event EventHandler VideoScreenPositionChangedEvent;

        event EventHandler ImageScreenPositionChangedEvent;

        event EventHandler WebScreenPositionChangedEvent;

        event EventHandler ShowMediaItemCommandPanelChangedEvent;

        event EventHandler OperatingDateChangedEvent;

        event EventHandler MaxItemCountChangedEvent;

        event EventHandler ShowFreezeCommandChangedEvent;

        event EventHandler MagnifierChangedEvent;

        event EventHandler BrowserChangedEvent;
        
        bool ShouldPurgeBrowserCacheOnStartup { get; set; }

        string AppWindowPlacement { get; set; }

        List<string> RecentlyUsedMediaFolders { get; set; }

        string Culture { get; set; }

        bool CacheImages { get; set; }

        bool EmbeddedThumbnails { get; set; }

        bool ConfirmVideoStop { get; set; }

        bool AllowVideoScrubbing { get; set; }

        bool JwLibraryCompatibilityMode { get; set; }

        bool ShowFreezeCommand { get; set; }

        int MaxItemCount { get; set; }

        DateTime OperatingDate { get; set; }

        bool ShowMediaItemCommandPanel { get; set; }

        ScreenPosition VideoScreenPosition { get; set; }

        ScreenPosition ImageScreenPosition { get; set; }

        ScreenPosition WebScreenPosition { get; set; }

        bool IncludeBlankScreenItem { get; set; }

        bool UseInternalMediaTitles { get; set; }

        bool ShowVideoSubtitles { get; set; }

        bool AllowVideoPositionSeeking { get; set; }

        bool AllowVideoPause { get; set; }
        
        bool PermanentBackdrop { get; set; }

        RenderingMethod RenderingMethod { get; set; }

        string MediaMonitorId { get; set; }

        double BrowserZoomLevelIncrement { get; set; }
        
        LogEventLevel LogEventLevel { get; set; }

        bool AlwaysOnTop { get; set; }

        double MagnifierFrameThickness { get; set; }

        MagnifierShape MagnifierShape { get; set; }

        MagnifierSize MagnifierSize { get; set; }

        double MagnifierZoomLevel { get; set; }

        FadeSpeed ImageFadeSpeed { get; set; }

        ImageFadeType ImageFadeType { get; set; }

        bool AutoRotateImages { get; set; }

        string MediaFolder { get; set; }

        bool IsMediaMonitorSpecified { get; }

        bool AllowMirror { get; set; }

        bool UseMirrorByDefault { get; set; }

        double MirrorZoom { get; set; }

        char MirrorHotKey { get; set; }

        void SetCommandLineMediaFolder(string folder);

        bool IsCommandLineMediaFolderSpecified();

        void Save();
    }
}
