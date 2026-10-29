using System.Text.Json;
using Moq;
using OnlyM.Core.Models;
using OnlyM.Core.Services.Options;
using OnlyM.Models;
using OnlyM.Services.RemoteControl;

namespace OnlyM.Tests;

/// <summary>
/// Tests for the static helpers of the HTTP remote: the /api/status and /api/media payloads, the presentation-clicker
/// rule for slideshows, the /api/seek rules, and the "port already in use" detection.
/// </summary>
public sealed class RemoteControlServiceTests
{
    [Theory]
    [InlineData(0, 5, false, 1, true)] // first slide: Next moves on, Previous goes to the previous media item
    [InlineData(2, 5, false, 1, true)]
    [InlineData(2, 5, false, -1, true)]
    [InlineData(0, 5, false, -1, false)]
    [InlineData(4, 5, false, 1, false)] // last slide: Next goes to the next media item
    [InlineData(4, 5, true, 1, true)] // looping slideshow wraps around
    [InlineData(0, 5, true, -1, true)]
    public void ShouldChangeSlide_FollowsSlideButtons(int index, int count, bool loop, int step, bool expected)
    {
        var item = CreateSlideshow(index, count, loop);

        Assert.Equal(expected, RemoteControlService.ShouldChangeSlide(item, step));
    }

    [Fact]
    public void ShouldChangeSlide_FalseForInactiveSlideshowOtherMediaOrNothing()
    {
        var inactive = CreateSlideshow(1, 5, loop: false);
        inactive.IsMediaActive = false;
        var video = new MediaItem
        {
            MediaType = new SupportedMediaType { Classification = MediaClassification.Video },
            IsMediaActive = true,
        };

        Assert.False(RemoteControlService.ShouldChangeSlide(inactive, 1));
        Assert.False(RemoteControlService.ShouldChangeSlide(video, 1));
        Assert.False(RemoteControlService.ShouldChangeSlide(null, 1));
    }

    [Fact]
    public void BuildStatus_Slideshow_IncludesSlidePosition()
    {
        var item = CreateSlideshow(2, 8, loop: false);
        item.IsRollingSlideshow = true;

        using var json = Parse(RemoteControlService.BuildStatus(item, 40, volumeEnabled: true));
        var root = json.RootElement;

        Assert.True(root.GetProperty("isPlaying").GetBoolean());
        Assert.Equal(item.Id.ToString(), root.GetProperty("currentItemId").GetString());
        Assert.Equal(40, root.GetProperty("volume").GetInt32());
        Assert.True(root.GetProperty("volumeEnabled").GetBoolean());
        Assert.Equal(2, root.GetProperty("currentSlideIndex").GetInt32());
        Assert.Equal(8, root.GetProperty("slideCount").GetInt32());
        Assert.True(root.GetProperty("isRolling").GetBoolean());
    }

    [Fact]
    public void BuildStatus_NotASlideshow_SlideFieldsAreNull()
    {
        var video = new MediaItem
        {
            MediaType = new SupportedMediaType { Classification = MediaClassification.Video },
            IsMediaActive = true,
        };

        using var json = Parse(RemoteControlService.BuildStatus(video, 100, volumeEnabled: false));
        var root = json.RootElement;

        Assert.False(root.GetProperty("volumeEnabled").GetBoolean());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("currentSlideIndex").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("slideCount").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("isRolling").ValueKind);
    }

    [Fact]
    public void BuildStatus_NothingPlaying()
    {
        using var json = Parse(RemoteControlService.BuildStatus(null, 0, volumeEnabled: true));
        var root = json.RootElement;

        Assert.False(root.GetProperty("isPlaying").GetBoolean());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("currentItemId").ValueKind);
        Assert.Equal(0, root.GetProperty("volume").GetInt32());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("slideCount").ValueKind);
    }

    [Theory]
    [InlineData(32, true)] // ERROR_SHARING_VIOLATION: another process listens on the port
    [InlineData(183, true)] // ERROR_ALREADY_EXISTS: the URL prefix is already registered
    [InlineData(5, false)] // access denied: handled by the localhost fallback instead
    [InlineData(0, false)]
    public void IsAddressInUse_RecognisesPortConflicts(int errorCode, bool expected)
    {
        Assert.Equal(expected, RemoteControlService.IsAddressInUse(errorCode));
    }

    [Fact]
    public void CloudflaredDownload_IsPinnedPerArchitecture()
    {
        var (url64, sha64) = CloudflaredInstaller.GetDownload(is64BitOperatingSystem: true);
        var (url32, sha32) = CloudflaredInstaller.GetDownload(is64BitOperatingSystem: false);

        Assert.Equal("https", url64.Scheme);
        Assert.Equal("github.com", url64.Host);
        Assert.EndsWith($"/{CloudflaredInstaller.Version}/cloudflared-windows-amd64.exe", url64.AbsolutePath, StringComparison.Ordinal);
        Assert.EndsWith($"/{CloudflaredInstaller.Version}/cloudflared-windows-386.exe", url32.AbsolutePath, StringComparison.Ordinal);
        Assert.Matches("^[0-9a-f]{64}$", sha64);
        Assert.Matches("^[0-9a-f]{64}$", sha32);
        Assert.NotEqual(sha64, sha32);
    }

    [Fact]
    public async Task CloudflaredHash_MatchesOnlyTheExpectedContent()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "abc", TestContext.Current.CancellationToken);

            // SHA-256 of "abc"
            const string expected = "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD";
            Assert.True(await CloudflaredInstaller.HasSha256Async(path, expected, TestContext.Current.CancellationToken));
            Assert.True(await CloudflaredInstaller.HasSha256Async(path, expected.ToLowerInvariant(), TestContext.Current.CancellationToken));
            Assert.False(await CloudflaredInstaller.HasSha256Async(path, new string('0', 64), TestContext.Current.CancellationToken));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("12.5", 125)]
    [InlineData("0", 0)]
    [InlineData("59.94", 599)]
    [InlineData("600", 600)] // clamped to the 60 s duration
    [InlineData("9999999", 600)]
    public void PlanSeek_WhenPaused_ClampsToDuration(string seconds, int expectedDeciseconds)
    {
        var video = CreateMedia(MediaClassification.Video, active: true, paused: true);

        var (statusCode, error, deciseconds) = RemoteControlService.PlanSeek(video, seconds);

        Assert.Equal(200, statusCode);
        Assert.Null(error);
        Assert.Equal(expectedDeciseconds, deciseconds);
    }

    [Fact]
    public void PlanSeek_PausedAudio_IsAllowed()
    {
        var audio = CreateMedia(MediaClassification.Audio, active: true, paused: true);

        Assert.Equal(200, RemoteControlService.PlanSeek(audio, "3").StatusCode);
    }

    [Fact]
    public void PlanSeek_NotStartedItem_FollowsOperatorSlider()
    {
        // The operator shows the slider (start position) for items that aren't playing; the remote only seeks the
        // active item, but the rule itself agrees with IsSliderVisible.
        var video = CreateMedia(MediaClassification.Video, active: false, paused: false);

        Assert.True(video.IsSliderVisible);
        Assert.Equal(200, RemoteControlService.PlanSeek(video, "5").StatusCode);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("-0.5")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1e3")]
    [InlineData("12,5")]
    [InlineData("+5")]
    public void PlanSeek_BadInput_Is400(string seconds)
    {
        var video = CreateMedia(MediaClassification.Video, active: true, paused: true);

        var (statusCode, error, _) = RemoteControlService.PlanSeek(video, seconds);

        Assert.Equal(400, statusCode);
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void PlanSeek_BadInput_Is400_EvenWhenNothingIsPlaying()
    {
        Assert.Equal(400, RemoteControlService.PlanSeek(null, "-3").StatusCode);
    }

    [Fact]
    public void PlanSeek_WhilePlaying_Is409()
    {
        var video = CreateMedia(MediaClassification.Video, active: true, paused: false);

        var (statusCode, error, _) = RemoteControlService.PlanSeek(video, "10");

        Assert.Equal(409, statusCode);
        Assert.Equal(OnlyM.Properties.Resources.REMOTE_CONTROL_API_PAUSE_TO_SCRUB, error);
    }

    [Fact]
    public void PlanSeek_NotAllowed_Is409()
    {
        var noSeeking = CreateMedia(MediaClassification.Video, active: true, paused: true);
        noSeeking.AllowPositionSeeking = false; // "Allow position seeking" is off in Settings
        var image = CreateMedia(MediaClassification.Image, active: true, paused: false);
        var slideshow = CreateSlideshow(0, 3, loop: false);

        Assert.Equal(409, RemoteControlService.PlanSeek(noSeeking, "1").StatusCode);
        Assert.Equal(409, RemoteControlService.PlanSeek(image, "1").StatusCode);
        Assert.Equal(409, RemoteControlService.PlanSeek(slideshow, "1").StatusCode);
        Assert.Equal(409, RemoteControlService.PlanSeek(null, "1").StatusCode);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void BuildStatus_CanSeek_OnlyWhenPausedAndSeekable(bool paused, bool disableSeeking, bool expected)
    {
        var video = CreateMedia(MediaClassification.Video, active: true, paused: paused);
        video.AllowPositionSeeking = !disableSeeking;

        using var json = Parse(RemoteControlService.BuildStatus(video, 100, volumeEnabled: false));

        Assert.Equal(expected, json.RootElement.GetProperty("canSeek").GetBoolean());
        Assert.Equal(60, json.RootElement.GetProperty("durationSeconds").GetDouble());
        Assert.Equal(25, json.RootElement.GetProperty("positionSeconds").GetDouble());
    }

    [Fact]
    public void BuildStatus_NothingPlaying_CannotSeek()
    {
        using var json = Parse(RemoteControlService.BuildStatus(null, 100, volumeEnabled: false));

        Assert.False(json.RootElement.GetProperty("canSeek").GetBoolean());
    }

    [Fact]
    public void BuildMediaEntry_Slideshow_HasSlideCountAndOperatorSubtitle()
    {
        var slideshow = CreateSlideshow(0, 6, loop: false);
        slideshow.IsMediaActive = false;
        slideshow.Title = "Announcements";
        var single = CreateSlideshow(0, 1, loop: false);

        using var json = Parse(RemoteControlService.BuildMediaEntry(slideshow));
        using var singleJson = Parse(RemoteControlService.BuildMediaEntry(single));

        Assert.Equal(6, json.RootElement.GetProperty("slideCount").GetInt32());
        Assert.Equal(
            OnlyM.Properties.Resources.CONTAINS_X_SLIDES.Replace("{0}", "6", StringComparison.Ordinal),
            json.RootElement.GetProperty("subtitle").GetString());
        Assert.Equal(slideshow.SlideshowProgressString, json.RootElement.GetProperty("subtitle").GetString());
        Assert.Equal("Announcements", json.RootElement.GetProperty("name").GetString());
        Assert.Equal("Slideshow", json.RootElement.GetProperty("type").GetString());
        Assert.Equal(OnlyM.Properties.Resources.CONTAINS_1_SLIDE, singleJson.RootElement.GetProperty("subtitle").GetString());
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)] // operator volume control hidden: no invisible volume changes from the remote
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void IsRemoteVolumeEnabled_NeedsBothOptions(bool showRemoteVolume, bool showOperatorVolume, bool expected)
    {
        var options = new Mock<IOptionsService>();
        options.SetupGet(x => x.ShowRemoteVolumeControl).Returns(showRemoteVolume);
        options.SetupGet(x => x.ShowVolumeControl).Returns(showOperatorVolume);

        Assert.Equal(expected, RemoteControlService.IsRemoteVolumeEnabled(options.Object));
    }

    [Fact]
    public void BuildMediaEntry_OtherMedia_HasNoSlideCount()
    {
        var video = CreateMedia(MediaClassification.Video, active: false, paused: false);
        var uncounted = CreateSlideshow(0, 0, loop: false); // slides not counted yet

        using var json = Parse(RemoteControlService.BuildMediaEntry(video));
        using var uncountedJson = Parse(RemoteControlService.BuildMediaEntry(uncounted));

        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("slideCount").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("subtitle").ValueKind);
        Assert.Equal(60, json.RootElement.GetProperty("durationSeconds").GetDouble());
        Assert.Equal(0, uncountedJson.RootElement.GetProperty("slideCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, uncountedJson.RootElement.GetProperty("subtitle").ValueKind);
    }

    private static MediaItem CreateMedia(MediaClassification classification, bool active, bool paused) => new()
    {
        Id = Guid.NewGuid(),
        MediaType = new SupportedMediaType { Classification = classification },
        IsMediaActive = active,
        IsPaused = paused,
        AllowPositionSeeking = true,
        DurationDeciseconds = 600,
        PlaybackPositionDeciseconds = 250,
    };

    private static MediaItem CreateSlideshow(int index, int count, bool loop) => new()
    {
        Id = Guid.NewGuid(),
        MediaType = new SupportedMediaType { Classification = MediaClassification.Slideshow },
        IsMediaActive = true,
        SlideshowCount = count,
        CurrentSlideshowIndex = index,
        SlideshowLoop = loop,
    };

    private static JsonDocument Parse(object status) => JsonDocument.Parse(RemoteControlService.SerializeJson(status));
}
