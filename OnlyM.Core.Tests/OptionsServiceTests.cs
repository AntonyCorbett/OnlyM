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
        _service.MaxItemCountChangedEvent += (_, _) => eventRaised = true;
        _service.MaxItemCount = 42;
        Assert.Equal(42, _service.MaxItemCount);
        Assert.True(eventRaised);
    }

    [Fact]
    public void OperatingDate_GetSet_Works()
    {
        var eventRaised = false;
        _service.OperatingDateChangedEvent += (_, _) => eventRaised = true;
        var date = DateTime.Today.AddDays(1);
        _service.OperatingDate = date;
        Assert.Equal(date, _service.OperatingDate);
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
        _service.IncludeBlankScreenItemChangedEvent += (_, _) => eventRaised = true;
        _service.IncludeBlankScreenItem = true;
        Assert.True(_service.IncludeBlankScreenItem);
        Assert.True(eventRaised);
    }

    [Fact]
    public void UseInternalMediaTitles_GetSet_Works()
    {
        var eventRaised = false;
        _service.UseInternalMediaTitlesChangedEvent += (_, _) => eventRaised = true;
        _service.UseInternalMediaTitles = true;
        Assert.True(_service.UseInternalMediaTitles);
        Assert.True(eventRaised);
    }

    [Fact]
    public void ShowVideoSubtitles_GetSet_Works()
    {
        var eventRaised = false;
        _service.ShowSubtitlesChangedEvent += (_, _) => eventRaised = true;
        _service.ShowVideoSubtitles = true;
        Assert.True(_service.ShowVideoSubtitles);
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
        _service.PermanentBackdropChangedEvent += (_, _) => eventRaised = true;
        _service.PermanentBackdrop = true;
        Assert.True(_service.PermanentBackdrop);
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
        _service.MediaMonitorChangedEvent += (_, _) => eventRaised = true;
        _service.MediaMonitorId = "monitor";
        Assert.Equal("monitor", _service.MediaMonitorId);
        Assert.True(eventRaised);
    }

    [Fact]
    public void MediaWindowed_GetSet_Works()
    {
        var eventRaised = false;
        _service.MediaMonitorChangedEvent += (_, _) => eventRaised = true;
        _service.MediaWindowed = true;
        Assert.True(_service.MediaWindowed);
        Assert.True(eventRaised);
    }

    [Fact]
    public void MediaWindowSize_GetSet_Works()
    {
        var eventRaised = false;
        _service.MediaMonitorChangedEvent += (_, _) => eventRaised = true;
        var size = new Size(100, 100);
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
        _service.BrowserChangedEvent += (_, _) => eventRaised = true;
        _service.BrowserZoomLevelIncrement = 1.5;
        Assert.Equal(1.5, _service.BrowserZoomLevelIncrement);
        Assert.True(eventRaised);
    }

    [Fact]
    public void LogEventLevel_GetSet_Works()
    {
        _service.LogEventLevel = LogEventLevel.Warning;
        Assert.Equal(LogEventLevel.Warning, _service.LogEventLevel);
        _logLevelSwitchServiceMock.Verify(x => x.SetMinimumLevel(LogEventLevel.Warning), Times.Once);
    }

    [Fact]
    public void AlwaysOnTop_GetSet_Works()
    {
        var eventRaised = false;
        _service.AlwaysOnTopChangedEvent += (_, _) => eventRaised = true;
        _service.AlwaysOnTop = true;
        Assert.True(_service.AlwaysOnTop);
        Assert.True(eventRaised);
    }

    [Fact]
    public void MagnifierFrameThickness_GetSet_Works()
    {
        var eventRaised = false;
        _service.MagnifierChangedEvent += (_, _) => eventRaised = true;
        _service.MagnifierFrameThickness = 2.5;
        Assert.Equal(2.5, _service.MagnifierFrameThickness);
        Assert.True(eventRaised);
    }

    [Fact]
    public void MagnifierShape_GetSet_Works()
    {
        var eventRaised = false;
        _service.MagnifierChangedEvent += (_, _) => eventRaised = true;
        _service.MagnifierShape = MagnifierShape.Circle;
        Assert.Equal(MagnifierShape.Circle, _service.MagnifierShape);
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
        _service.MagnifierChangedEvent += (_, _) => eventRaised = true;
        _service.MagnifierZoomLevel = 1.2;
        Assert.Equal(1.2, _service.MagnifierZoomLevel);
        Assert.True(eventRaised);
    }

    [Fact]
    public void ImageFadeSpeed_GetSet_Works()
    {
        var eventRaised = false;
        _service.ImageFadeSpeedChangedEvent += (_, _) => eventRaised = true;
        _service.ImageFadeSpeed = FadeSpeed.Fast;
        Assert.Equal(FadeSpeed.Fast, _service.ImageFadeSpeed);
        Assert.True(eventRaised);
    }

    [Fact]
    public void ImageFadeType_GetSet_Works()
    {
        var eventRaised = false;
        _service.ImageFadeTypeChangedEvent += (_, _) => eventRaised = true;
        _service.ImageFadeType = ImageFadeType.FadeIn;
        Assert.Equal(ImageFadeType.FadeIn, _service.ImageFadeType);
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
        _service.UseMirrorByDefault = true;
        Assert.True(_service.UseMirrorByDefault);
    }

    [Fact]
    public void MirrorZoom_GetSet_Works()
    {
        _service.MirrorZoom = 1.5;
        Assert.Equal(1.5, _service.MirrorZoom);
    }

    [Fact]
    public void MirrorHotKey_GetSet_Works()
    {
        _service.MirrorHotKey = 'X';
        Assert.Equal('X', _service.MirrorHotKey);
    }

    [Fact]
    public void Save_DoesNotThrow()
    {
        _service.Save();
    }
}
