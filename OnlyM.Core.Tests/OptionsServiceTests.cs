using System.Windows;
using Moq;
using OnlyM.Core.Models;
using OnlyM.Core.Services.CommandLine;
using OnlyM.Core.Services.Options;
using Serilog.Events;

namespace OnlyM.Core.Tests;

public class OptionsServiceTests
{
    private readonly Mock<ILogLevelSwitchService> _logLevelSwitchServiceMock;
    private readonly OptionsService _service;

    public OptionsServiceTests()
    {
        _logLevelSwitchServiceMock = new Mock<ILogLevelSwitchService>();
        var commandLineServiceMock = new Mock<ICommandLineService>();
        _service = new OptionsService(_logLevelSwitchServiceMock.Object, commandLineServiceMock.Object);
    }

    [Fact]
    public void SetCommandLineMediaFolder_SetsValue()
    {
        _service.SetCommandLineMediaFolder("folder");
        Assert.True(_service.IsCommandLineMediaFolderSpecified());
    }

    [Fact]
    public void IsCommandLineMediaFolderSpecified_ReturnsFalse_WhenNotSet()
    {
        Assert.False(_service.IsCommandLineMediaFolderSpecified());
    }

    [Fact]
    public void ShouldPurgeBrowserCacheOnStartup_GetSet_Works()
    {
        _service.ShouldPurgeBrowserCacheOnStartup = true;
        Assert.True(_service.ShouldPurgeBrowserCacheOnStartup);
    }

    [Fact]
    public void AppWindowPlacement_GetSet_Works()
    {
        _service.AppWindowPlacement = "placement";
        Assert.Equal("placement", _service.AppWindowPlacement);
    }

    [Fact]
    public void MediaWindowPlacement_GetSet_Works()
    {
        _service.MediaWindowPlacement = "media";
        Assert.Equal("media", _service.MediaWindowPlacement);
    }

    [Fact]
    public void RecentlyUsedMediaFolders_GetSet_Works()
    {
        var folders = new List<string> { "a", "b" };
        _service.RecentlyUsedMediaFolders = folders;
        Assert.Equal(folders, _service.RecentlyUsedMediaFolders);
    }

    [Fact]
    public void Culture_GetSet_Works()
    {
        _service.Culture = "en-US";
        Assert.Equal("en-US", _service.Culture);
    }

    [Fact]
    public void CacheImages_GetSet_Works()
    {
        _service.CacheImages = true;
        Assert.True(_service.CacheImages);
    }

    [Fact]
    public void EmbeddedThumbnails_GetSet_Works()
    {
        _service.EmbeddedThumbnails = true;
        Assert.True(_service.EmbeddedThumbnails);
    }

    [Fact]
    public void ConfirmVideoStop_GetSet_Works()
    {
        _service.ConfirmVideoStop = true;
        Assert.True(_service.ConfirmVideoStop);
    }

    [Fact]
    public void AllowVideoScrubbing_GetSet_Works()
    {
        _service.AllowVideoScrubbing = true;
        Assert.True(_service.AllowVideoScrubbing);
    }

    [Fact]
    public void ShowFreezeCommand_GetSet_Works()
    {
        var eventRaised = false;
        var currentValue = _service.ShowFreezeCommand;
        var newValue = !currentValue;

        _service.ShowFreezeCommandChangedEvent += (_, _) => eventRaised = true;
        _service.ShowFreezeCommand = newValue;
        Assert.Equal(newValue, _service.ShowFreezeCommand);
        Assert.True(eventRaised);
    }

    [Fact]
    public void MaxItemCount_GetSet_Works()
    {
        var eventRaised = false;
        var currentValue = _service.MaxItemCount;
        var newValue = currentValue + 1;

        _service.MaxItemCountChangedEvent += (_, _) => eventRaised = true;
        _service.MaxItemCount = newValue;
        Assert.Equal(newValue, _service.MaxItemCount);
        Assert.True(eventRaised);
    }

    [Fact]
    public void OperatingDate_GetSet_Works()
    {
        var eventRaised = false;
        var currentValue = _service.OperatingDate;
        var newValue = currentValue.AddDays(1);

        _service.OperatingDateChangedEvent += (_, _) => eventRaised = true;
        _service.OperatingDate = newValue;
        Assert.Equal(newValue, _service.OperatingDate);
        Assert.True(eventRaised);
    }

    [Fact]
    public void ShowMediaItemCommandPanel_GetSet_Works()
    {
        var eventRaised = false;
        var currentValue = _service.ShowMediaItemCommandPanel;
        var newValue = !currentValue;

        _service.ShowMediaItemCommandPanelChangedEvent += (_, _) => eventRaised = true;
        _service.ShowMediaItemCommandPanel = newValue;
        Assert.Equal(newValue, _service.ShowMediaItemCommandPanel);
        Assert.True(eventRaised);
    }

    [Fact]
    public void VideoScreenPosition_GetSet_Works()
    {
        var eventRaised = false;
        var newValue = new ScreenPosition(1, 1, 1, 1);

        _service.VideoScreenPositionChangedEvent += (_, _) => eventRaised = true;
        _service.VideoScreenPosition = newValue;
        Assert.Equal(newValue, _service.VideoScreenPosition);
        Assert.True(eventRaised);
    }

    [Fact]
    public void ImageScreenPosition_GetSet_Works()
    {
        var eventRaised = false;
        _service.ImageScreenPositionChangedEvent += (_, _) => eventRaised = true;
        var pos = new ScreenPosition(1, 1, 1, 1);
        _service.ImageScreenPosition = pos;
        Assert.Equal(pos, _service.ImageScreenPosition);
        Assert.True(eventRaised);
    }

    [Fact]
    public void WebScreenPosition_GetSet_Works()
    {
        var eventRaised = false;
        _service.WebScreenPositionChangedEvent += (_, _) => eventRaised = true;
        var pos = new ScreenPosition(1, 1, 1, 1);
        _service.WebScreenPosition = pos;
        Assert.Equal(pos, _service.WebScreenPosition);
        Assert.True(eventRaised);
    }

    [Fact]
    public void IncludeBlankScreenItem_GetSet_Works()
    {
        var eventRaised = false;
        var currentValue = _service.IncludeBlankScreenItem;
        var newValue = !currentValue;
        _service.IncludeBlankScreenItemChangedEvent += (_, _) => eventRaised = true;
        _service.IncludeBlankScreenItem = newValue;
        Assert.Equal(newValue, _service.IncludeBlankScreenItem);
        Assert.True(eventRaised);
    }

    [Fact]
    public void UseInternalMediaTitles_GetSet_Works()
    {
        var eventRaised = false;
        var currentValue = _service.UseInternalMediaTitles;
        var newValue = !currentValue;
        _service.UseInternalMediaTitlesChangedEvent += (_, _) => eventRaised = true;
        _service.UseInternalMediaTitles = newValue;
        Assert.Equal(newValue, _service.UseInternalMediaTitles);
        Assert.True(eventRaised);
    }

    [Fact]
    public void ShowVideoSubtitles_GetSet_Works()
    {
        var eventRaised = false;
        var currentValue = _service.ShowVideoSubtitles;
        var newValue = !currentValue;

        _service.ShowSubtitlesChangedEvent += (_, _) => eventRaised = true;
        _service.ShowVideoSubtitles = newValue;
        Assert.Equal(newValue, _service.ShowVideoSubtitles);
        Assert.True(eventRaised);
    }

    [Fact]
    public void AllowVideoPositionSeeking_GetSet_Works()
    {
        var eventRaised = false;
        var currentValue = _service.AllowVideoPositionSeeking;
        var newValue = !currentValue;

        _service.AllowVideoPositionSeekingChangedEvent += (_, _) => eventRaised = true;
        _service.AllowVideoPositionSeeking = newValue;
        Assert.Equal(newValue, _service.AllowVideoPositionSeeking);
        Assert.True(eventRaised);
    }

    [Fact]
    public void AllowVideoPause_GetSet_Works()
    {
        var eventRaised = false;
        var currentValue = _service.AllowVideoPause;
        var newValue = !currentValue;

        _service.AllowVideoPauseChangedEvent += (_, _) => eventRaised = true;
        _service.AllowVideoPause = newValue;
        Assert.Equal(newValue, _service.AllowVideoPause);
        Assert.True(eventRaised);
    }

    [Fact]
    public void PermanentBackdrop_GetSet_Works()
    {
        var eventRaised = false;
        var currentValue = _service.PermanentBackdrop;
        var newValue = !currentValue;
        _service.PermanentBackdropChangedEvent += (_, _) => eventRaised = true;
        _service.PermanentBackdrop = newValue;
        Assert.Equal(newValue, _service.PermanentBackdrop);
        Assert.True(eventRaised);
    }

    [Fact]
    public void RenderingMethod_GetSet_Works()
    {
        var eventRaised = false;
        var currentValue = _service.RenderingMethod;
        var newValue = currentValue == RenderingMethod.Ffmpeg ? RenderingMethod.MediaFoundation : RenderingMethod.Ffmpeg;

        _service.RenderingMethodChangedEvent += (_, _) => eventRaised = true;
        _service.RenderingMethod = newValue;
        Assert.Equal(newValue, _service.RenderingMethod);
        Assert.True(eventRaised);
    }

    [Fact]
    public void MediaMonitorId_GetSet_Works()
    {
        var eventRaised = false;
        var currentValue = _service.MediaMonitorId;
        var newValue = currentValue == "monitor" ? "anotherMonitor" : "monitor";
        _service.MediaMonitorChangedEvent += (_, _) => eventRaised = true;
        _service.MediaMonitorId = newValue;
        Assert.Equal(newValue, _service.MediaMonitorId);
        Assert.True(eventRaised);
    }

    [Fact]
    public void MediaWindowed_GetSet_Works()
    {
        var eventRaised = false;
        var currentValue = _service.MediaWindowed;
        var newValue = !currentValue;
        _service.MediaMonitorChangedEvent += (_, _) => eventRaised = true;
        _service.MediaWindowed = newValue;
        Assert.Equal(newValue, _service.MediaWindowed);
        Assert.True(eventRaised);
    }

    [Fact]
    public void MediaWindowSize_GetSet_Works()
    {
        var eventRaised = false;
        _service.MediaMonitorChangedEvent += (_, _) => eventRaised = true;
        var size = new Size(1, 1);
        _service.MediaWindowSize = size;
        Assert.Equal(size, _service.MediaWindowSize);
        Assert.True(eventRaised);
    }

    [Fact]
    public void WindowedAlwaysOnTop_GetSet_Works()
    {
        var eventRaised = false;
        var currentValue = _service.WindowedAlwaysOnTop;
        var newValue = !currentValue;

        _service.WindowedMediaAlwaysOnTopChangedEvent += (_, _) => eventRaised = true;
        _service.WindowedAlwaysOnTop = newValue;
        Assert.Equal(newValue, _service.WindowedAlwaysOnTop);
        Assert.True(eventRaised);
    }

    [Fact]
    public void BrowserZoomLevelIncrement_GetSet_Works()
    {
        var eventRaised = false;
        var currentValue = _service.BrowserZoomLevelIncrement;
        var newValue = currentValue + 0.5;
        _service.BrowserChangedEvent += (_, _) => eventRaised = true;
        _service.BrowserZoomLevelIncrement = newValue;
        Assert.Equal(newValue, _service.BrowserZoomLevelIncrement);
        Assert.True(eventRaised);
    }

    [Fact]
    public void LogEventLevel_GetSet_Works()
    {
        var currentValue = _service.LogEventLevel;
        var newValue = currentValue == LogEventLevel.Information ? LogEventLevel.Warning : LogEventLevel.Information;

        _service.LogEventLevel = newValue;
        Assert.Equal(newValue, _service.LogEventLevel);
        _logLevelSwitchServiceMock.Verify(x => x.SetMinimumLevel(LogEventLevel.Warning), Times.Once);
    }

    [Fact]
    public void AlwaysOnTop_GetSet_Works()
    {
        var eventRaised = false;
        var currentValue = _service.AlwaysOnTop;
        var newValue = !currentValue;
        _service.AlwaysOnTopChangedEvent += (_, _) => eventRaised = true;
        _service.AlwaysOnTop = newValue;
        Assert.Equal(newValue, _service.AlwaysOnTop);
        Assert.True(eventRaised);
    }

    [Fact]
    public void MagnifierFrameThickness_GetSet_Works()
    {
        var eventRaised = false;
        var currentValue = _service.MagnifierFrameThickness;
        var newValue = currentValue + 1.0;

        _service.MagnifierChangedEvent += (_, _) => eventRaised = true;
        _service.MagnifierFrameThickness = newValue;
        Assert.Equal(newValue, _service.MagnifierFrameThickness);
        Assert.True(eventRaised);
    }

    [Fact]
    public void MagnifierShape_GetSet_Works()
    {
        var eventRaised = false;
        var currentValue = _service.MagnifierShape;
        var newValue = currentValue == MagnifierShape.Circle ? MagnifierShape.Square : MagnifierShape.Circle;

        _service.MagnifierChangedEvent += (_, _) => eventRaised = true;
        _service.MagnifierShape = newValue;
        Assert.Equal(newValue, _service.MagnifierShape);
        Assert.True(eventRaised);
    }

    [Fact]
    public void MagnifierSize_GetSet_Works()
    {
        var eventRaised = false;
        var currentValue = _service.MagnifierSize;
        var newValue = currentValue == MagnifierSize.Large ? MagnifierSize.Medium : MagnifierSize.Large;

        _service.MagnifierChangedEvent += (_, _) => eventRaised = true;
        _service.MagnifierSize = newValue;
        Assert.Equal(newValue, _service.MagnifierSize);
        Assert.True(eventRaised);
    }

    [Fact]
    public void MagnifierZoomLevel_GetSet_Works()
    {
        var eventRaised = false;
        var currentValue = _service.MagnifierZoomLevel;
        var newValue = currentValue + 0.1;

        _service.MagnifierChangedEvent += (_, _) => eventRaised = true;
        _service.MagnifierZoomLevel = newValue;
        Assert.Equal(newValue, _service.MagnifierZoomLevel);
        Assert.True(eventRaised);
    }

    [Fact]
    public void ImageFadeSpeed_GetSet_Works()
    {
        var eventRaised = false;
        var currentValue = _service.ImageFadeSpeed;
        var newValue = currentValue == FadeSpeed.Slow ? FadeSpeed.Fast : FadeSpeed.Slow;

        _service.ImageFadeSpeedChangedEvent += (_, _) => eventRaised = true;
        _service.ImageFadeSpeed = newValue;
        Assert.Equal(newValue, _service.ImageFadeSpeed);
        Assert.True(eventRaised);
    }

    [Fact]
    public void ImageFadeType_GetSet_Works()
    {
        var eventRaised = false;
        var currentValue = _service.ImageFadeType;
        var newValue = currentValue == ImageFadeType.FadeIn ? ImageFadeType.FadeOut : ImageFadeType.FadeIn;
        _service.ImageFadeTypeChangedEvent += (_, _) => eventRaised = true;
        _service.ImageFadeType = newValue;
        Assert.Equal(newValue, _service.ImageFadeType);
        Assert.True(eventRaised);
    }

    [Fact]
    public void AutoRotateImages_GetSet_Works()
    {
        var eventRaised = false;
        var currentValue = _service.AutoRotateImages;
        var newValue = !currentValue;

        _service.AutoRotateChangedEvent += (_, _) => eventRaised = true;
        _service.AutoRotateImages = newValue;
        Assert.Equal(newValue, _service.AutoRotateImages);
        Assert.True(eventRaised);
    }

    [Fact]
    public void MediaFolder_GetSet_Works()
    {
        var eventRaised = false;
        _service.MediaFolderChangedEvent += (_, _) => eventRaised = true;
        _service.MediaFolder = "folder";
        Assert.Equal("folder", _service.MediaFolder);
        Assert.True(eventRaised);
    }

    [Fact]
    public void IsMediaMonitorSpecified_ReturnsExpected()
    {
        _service.MediaMonitorId = "monitor";
        Assert.True(_service.IsMediaMonitorSpecified);
    }

    [Fact]
    public void AllowMirror_GetSet_Works()
    {
        var eventRaised = false;
        var currentValue = _service.AllowMirror;
        var newValue = !currentValue;

        _service.AllowMirrorChangedEvent += (_, _) => eventRaised = true;
        _service.AllowMirror = newValue;
        Assert.Equal(newValue, _service.AllowMirror);
        Assert.True(eventRaised);
    }

    [Fact]
    public void UseMirrorByDefault_GetSet_Works()
    {
        var currentValue = _service.UseMirrorByDefault;
        var newValue = !currentValue;
        _service.UseMirrorByDefault = newValue;
        Assert.Equal(newValue, _service.UseMirrorByDefault);
    }

    [Fact]
    public void MirrorZoom_GetSet_Works()
    {
        var currentValue = _service.MirrorZoom;
        var newValue = currentValue + 0.5;

        _service.MirrorZoom = newValue;
        Assert.Equal(newValue, _service.MirrorZoom);
    }

    [Fact]
    public void MirrorZoom_GetSet_Works_With_Value_LessThan_Minimum()
    {
        _service.MirrorZoom = Options.MinMirrorZoom - 0.1;
        Assert.Equal(Options.MinMirrorZoom, _service.MirrorZoom);
    }

    [Fact]
    public void MirrorZoom_GetSet_Works_With_Value_MoreThan_Maximum()
    {
        _service.MirrorZoom = Options.MaxMirrorZoom + 0.1;
        Assert.Equal(Options.MaxMirrorZoom, _service.MirrorZoom);
    }

    [Fact]
    public void MirrorZoom_GetSet_Works_With_Normalisation()
    {
        _service.MirrorZoom = Options.DefaultMirrorZoom + 0.01;
        Assert.Equal(Options.DefaultMirrorZoom, _service.MirrorZoom);

        _service.MirrorZoom = Options.DefaultMirrorZoom - 0.05;
        Assert.Equal(Options.DefaultMirrorZoom, _service.MirrorZoom);

        _service.MirrorZoom = Options.DefaultMirrorZoom + 0.1;
        Assert.Equal(Options.DefaultMirrorZoom + 0.1, _service.MirrorZoom);

        _service.MirrorZoom = Options.DefaultMirrorZoom + 0.18;
        Assert.Equal(Options.DefaultMirrorZoom + 0.2, _service.MirrorZoom);
    }

    [Fact]
    public void MirrorHotKey_GetSet_Works()
    {
        var currentValue = _service.MirrorHotKey;
        var newValue = currentValue == 'X' ? 'Y' : 'X';

        _service.MirrorHotKey = newValue;
        Assert.Equal(newValue, _service.MirrorHotKey);
    }

    [Fact]
    public void ShowVolumeControl_TurnedOff_ResetsVolumeTo100()
    {
        var originalShow = _service.ShowVolumeControl;
        var originalVolume = _service.Volume;
        try
        {
            _service.ShowVolumeControl = true;
            _service.Volume = 40;

            var showChanged = 0;
            var volumeChanged = 0;
            _service.ShowVolumeControlChangedEvent += (_, _) => showChanged++;
            _service.VolumeChangedEvent += (_, _) => volumeChanged++;

            _service.ShowVolumeControl = false;

            Assert.False(_service.ShowVolumeControl);
            Assert.Equal(100, _service.Volume);
            Assert.Equal(1, showChanged);
            Assert.Equal(1, volumeChanged);
        }
        finally
        {
            _service.ShowVolumeControl = true;
            _service.Volume = originalVolume;
            _service.ShowVolumeControl = originalShow;
        }
    }

    [Fact]
    public void ShowRemoteVolumeControl_GetSet_RaisesEvent()
    {
        var original = _service.ShowRemoteVolumeControl;
        try
        {
            var changed = 0;
            _service.ShowRemoteVolumeControlChangedEvent += (_, _) => changed++;

            _service.ShowRemoteVolumeControl = !original;
            _service.ShowRemoteVolumeControl = !original;

            Assert.Equal(!original, _service.ShowRemoteVolumeControl);
            Assert.Equal(1, changed);
        }
        finally
        {
            _service.ShowRemoteVolumeControl = original;
        }
    }

    [Fact]
    public void RemoteControlChangedEvent_OnlyForEnableAndMode()
    {
        // RemoteControlChangedEvent makes the remote service re-apply its settings (it is idempotent, but a
        // spurious event must never come from display options such as the remote volume control).
        var originalRemoteVolume = _service.ShowRemoteVolumeControl;
        var originalShowVolume = _service.ShowVolumeControl;
        var originalVolume = _service.Volume;
        var originalPin = _service.RemoteControlPin;
        var originalPort = _service.RemoteControlPort;
        var originalScrub = _service.AllowVideoScrubbing;
        var originalSeek = _service.AllowVideoPositionSeeking;
        var originalEnabled = _service.RemoteControlEnabled;
        var originalMode = _service.RemoteControlMode;
        try
        {
            var changed = 0;
            _service.RemoteControlChangedEvent += (_, _) => changed++;

            _service.ShowRemoteVolumeControl = !originalRemoteVolume;
            _service.ShowRemoteVolumeControl = originalRemoteVolume;
            _service.ShowVolumeControl = !originalShowVolume;
            _service.ShowVolumeControl = originalShowVolume;
            _service.Volume = originalVolume == 40 ? 60 : 40;
            _service.RemoteControlPin = originalPin == "135790" ? "246802" : "135790";
            _service.RemoteControlPort = originalPort == 8091 ? 8092 : 8091; // applied by SettingsViewModel
            _service.AllowVideoScrubbing = !originalScrub;
            _service.AllowVideoPositionSeeking = !originalSeek;
            Assert.Equal(0, changed);

            _service.RemoteControlEnabled = !originalEnabled;
            Assert.Equal(1, changed);
            _service.RemoteControlMode = originalMode == RemoteControlMode.Local ? RemoteControlMode.Cloudflare : RemoteControlMode.Local;
            Assert.Equal(2, changed);
        }
        finally
        {
            _service.ShowRemoteVolumeControl = originalRemoteVolume;
            _service.ShowVolumeControl = originalShowVolume;
            _service.Volume = originalVolume;
            _service.RemoteControlPin = originalPin;
            _service.RemoteControlPort = originalPort;
            _service.AllowVideoScrubbing = originalScrub;
            _service.AllowVideoPositionSeeking = originalSeek;
            _service.RemoteControlEnabled = originalEnabled;
            _service.RemoteControlMode = originalMode;
        }
    }

    [Fact]
    public void Options_VolumeControlsDefaultOff_AndPortIsUncommon()
    {
        var options = new Options();

        Assert.False(options.ShowVolumeControl);
        Assert.False(options.ShowRemoteVolumeControl);
        Assert.Equal(Options.DefaultRemoteControlPort, options.RemoteControlPort);
        Assert.NotEqual(8088, options.RemoteControlPort);
        Assert.NotEqual(8096, options.RemoteControlPort);
    }

    [Theory]
    [InlineData(false, 40, 100)]
    [InlineData(false, 0, 100)]
    [InlineData(true, 40, 40)]
    [InlineData(true, 0, 0)]
    [InlineData(true, 150, 100)]
    public void Options_Sanitize_ResetsVolumeWhenControlHidden(bool showVolumeControl, int volume, int expected)
    {
        var options = new Options { ShowVolumeControl = showVolumeControl, Volume = volume };

        options.Sanitize();

        Assert.Equal(expected, options.Volume);
    }

    [Fact]
    public void Options_Sanitize_InvalidPort_UsesDefault()
    {
        var options = new Options { RemoteControlPort = 0 };

        options.Sanitize();

        Assert.Equal(Options.DefaultRemoteControlPort, options.RemoteControlPort);
    }

    [Fact]
    public void Save_DoesNotThrow()
    {
        _service.Save();
    }
}
