namespace OnlyM.Core.Services.Options
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Newtonsoft.Json;
    using OnlyM.Core.Models;
    using OnlyM.Core.Utils;
    using Serilog.Events;

    public sealed class Options
    {
        private const int AbsoluteMaxItemCount = 200;
        private const int DefaultMaxItemCount = 50;
        
        private const double DefaultMagnifierZoomLevel = 0.5;
        private const double DefaultBrowserZoomLevelIncrement = 0.25;

        private const double MinBrowserFrameThickness = 0.0;
        private const double DefaultBrowserFrameThickness = 2.0;
        private const double MaxBrowserFrameThickness = 5.0;

        private const double MinMirrorZoom = 1.0;
        private const double MaxMirrorZoom = 2.0;
        private const double DefaultMirrorZoom = 1.0;
        private const char DefaultMirrorHotKey = 'Z';
        
        public Options()
        {
            // defaults
            AlwaysOnTop = true;
            LogEventLevel = LogEventLevel.Information;
            MediaFolder = FileUtils.GetOnlyMDefaultMediaFolder();
            ImageFadeType = ImageFadeType.CrossFade;
            ImageFadeSpeed = FadeSpeed.Normal;
            CacheImages = true;
            ShowVideoSubtitles = false;
            AllowVideoScrubbing = true;
            AllowVideoPause = true;
            AllowVideoPositionSeeking = true;
            PermanentBackdrop = true;
            JwLibraryCompatibilityMode = true;
            ConfirmVideoStop = false;
            MaxItemCount = DefaultMaxItemCount;
            MagnifierZoomLevel = DefaultMagnifierZoomLevel;
            BrowserZoomLevelIncrement = DefaultBrowserZoomLevelIncrement;
            MagnifierShape = MagnifierShape.Circle;
            MagnifierSize = MagnifierSize.Medium;
            MagnifierFrameThickness = DefaultBrowserFrameThickness;

            VideoScreenPosition = new ScreenPosition();
            ImageScreenPosition = new ScreenPosition();
            WebScreenPosition = new ScreenPosition();

            AllowMirror = true;
            MirrorZoom = DefaultMirrorZoom;

            Sanitize();
        }

        public bool ShouldPurgeBrowserCacheOnStartup { get; set; }

        public bool ShowMediaItemCommandPanel { get; set; }

        public bool ShowFreezeCommand { get; set; }

        public string MediaMonitorId { get; set; }

        public bool MediaWindowed { get; set; }

        public RenderingMethod RenderingMethod { get; set; }
        
        [JsonIgnore]
        public DateTime OperatingDate { get; set; }

        public int MaxItemCount { get; set; }

        public ScreenPosition VideoScreenPosition { get; set; }
        
        public ScreenPosition ImageScreenPosition { get; set; }

        public ScreenPosition WebScreenPosition { get; set; }

        public bool IncludeBlankScreenItem { get; set; }

        public bool UseInternalMediaTitles { get; set; }

        public bool PermanentBackdrop { get; set; }
        
        public bool AllowVideoPause { get; set; }
        
        public bool AllowVideoPositionSeeking { get; set; }
        
        public bool ShowVideoSubtitles { get; set; }

        public bool AllowVideoScrubbing { get; set; }

        public bool AlwaysOnTop { get; set; }
        
        public string AppWindowPlacement { get; set; }

        public string MediaWindowPlacement { get; set; }

        public bool JwLibraryCompatibilityMode { get; set; }

        public bool ConfirmVideoStop { get; set; }

        public string Culture { get; set; }

        public LogEventLevel LogEventLevel { get; set; }

        public bool AutoRotateImages { get; set; }
        
        public string MediaFolder { get; set; }
        
        public ImageFadeType ImageFadeType { get; set; }
        
        public FadeSpeed ImageFadeSpeed { get; set; }

        public double BrowserZoomLevelIncrement { get; set; }
        
        public MagnifierShape MagnifierShape { get; set; }

        public MagnifierSize MagnifierSize { get; set; }

        public double MagnifierZoomLevel { get; set; }

        public double MagnifierFrameThickness { get; set; }

        public bool EmbeddedThumbnails { get; set; }

        public bool CacheImages { get; set; }

        public List<string> RecentlyUsedMediaFolders { get; set; } = new List<string>();

        public bool AllowMirror { get; set; }

        public bool UseMirrorByDefault { get; set; }

        public double MirrorZoom { get; set; }

        public char MirrorHotKey { get; set; }

        /// <summary>
        /// Validates the data, correcting automatically as required
        /// </summary>
        public void Sanitize()
        {
            if (!Directory.Exists(MediaFolder))
            {
                MediaFolder = FileUtils.GetOnlyMDefaultMediaFolder();
            }

            if (JwLibraryCompatibilityMode)
            {
                PermanentBackdrop = false;
            }

            VideoScreenPosition.Sanitize();
            ImageScreenPosition.Sanitize();
            WebScreenPosition.Sanitize();

            if (!RecentlyUsedMediaFolders.Any())
            {
                RecentlyUsedMediaFolders.Add(!string.IsNullOrEmpty(MediaFolder)
                    ? MediaFolder
                    : FileUtils.GetOnlyMDefaultMediaFolder());
            }

            for (int n = RecentlyUsedMediaFolders.Count - 1; n >= 0; --n)
            {
                var folder = RecentlyUsedMediaFolders[n];
                if (!Directory.Exists(folder))
                {
                    RecentlyUsedMediaFolders.RemoveAt(n);
                }
            }

            // media calendar date is always set to today
            // on startup.
            OperatingDate = DateTime.Today;

            if (MaxItemCount > AbsoluteMaxItemCount)
            {
                MaxItemCount = AbsoluteMaxItemCount;
            }

            if (MaxItemCount <= 0)
            {
                MaxItemCount = 1;
            }

            if (MagnifierZoomLevel < 0 || MagnifierZoomLevel > 1.0)
            {
                MagnifierZoomLevel = DefaultMagnifierZoomLevel;
            }

            if (BrowserZoomLevelIncrement < 0 || BrowserZoomLevelIncrement > 1)
            {
                BrowserZoomLevelIncrement = DefaultBrowserZoomLevelIncrement;
            }

            if (MagnifierFrameThickness < MinBrowserFrameThickness ||
                MagnifierFrameThickness > MaxBrowserFrameThickness)
            {
                MagnifierFrameThickness = DefaultBrowserFrameThickness;
            }

            if (MirrorZoom < MinMirrorZoom || MirrorZoom > MaxMirrorZoom)
            {
                MirrorZoom = DefaultMirrorZoom;
            }

            if (MirrorHotKey < 'A' || MirrorHotKey > 'Z')
            {
                MirrorHotKey = DefaultMirrorHotKey;
            }
        }
    }
}
