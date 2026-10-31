using System.Text;
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

    [Theory]
    [InlineData(MediaClassification.Image, "photo.jpg", true)]
    [InlineData(MediaClassification.Video, "clip.mp4", true)]
    [InlineData(MediaClassification.Slideshow, "slides.omslide", true)]
    [InlineData(MediaClassification.Audio, "song.mp3", false)] // only a generic icon
    [InlineData(MediaClassification.Web, "notes.pdf", false)]
    [InlineData(MediaClassification.Web, "NOTES.PDF", false)]
    [InlineData(MediaClassification.Web, "example.url", false)] // web link
    [InlineData(MediaClassification.Unknown, "file.xyz", false)]
    public void CanPreview_OnlyImagesVideoAndSlideshows(MediaClassification classification, string fileName, bool expected)
    {
        var item = new MediaItem
        {
            Id = Guid.NewGuid(),
            MediaType = new SupportedMediaType { Classification = classification },
            FilePath = Path.Combine("C:\\media", fileName),
        };

        Assert.Equal(expected, RemoteControlService.CanPreview(item));
        using var json = Parse(RemoteControlService.BuildMediaEntry(item));
        Assert.Equal(expected, json.RootElement.GetProperty("canPreview").GetBoolean());
    }

    [Fact]
    public void CanPreview_NotWithoutAFile()
    {
        Assert.False(RemoteControlService.CanPreview(CreateMedia(MediaClassification.Image, active: false, paused: false)));
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

    [Theory]
    [InlineData(0)]
    [InlineData(1024)]
    public async Task ReadLimitedAsync_ReturnsBodyUpToTheLimit(int size)
    {
        var text = new string('7', size);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));

        Assert.Equal(text, await RemoteControlService.ReadLimitedAsync(stream, Encoding.UTF8, 1024));
    }

    [Fact]
    public async Task ReadLimitedAsync_ReturnsNullOverTheLimitWithoutReadingItAll()
    {
        using var stream = new MemoryStream(new byte[100_000]);

        Assert.Null(await RemoteControlService.ReadLimitedAsync(stream, Encoding.UTF8, 1024));
        Assert.Equal(1025, stream.Position);
    }

    [Theory]
    [InlineData(-1, 1, 0)] // nothing active: Next starts at the top
    [InlineData(-1, -1, 3)] // nothing active: Previous starts at the bottom
    [InlineData(0, 1, 2)] // skips hidden item 1
    [InlineData(2, -1, 0)]
    [InlineData(3, 1, 0)] // wraps around
    [InlineData(1, 1, 2)] // the active item is hidden: still steps from its position
    [InlineData(1, -1, 0)]
    public void FindRelativeItem_SkipsHiddenItems(int activeIndex, int step, int expectedIndex)
    {
        var items = Enumerable.Range(0, 4).Select(i => new MediaItem { Id = Guid.NewGuid(), IsVisible = i != 1 }).ToList();
        if (activeIndex >= 0)
        {
            items[activeIndex].IsMediaActive = true;
        }

        Assert.Equal(items[expectedIndex].Id, RemoteControlService.FindRelativeItem(items, step));
    }

    [Theory]
    [InlineData("{\"pin\":\"123456\"}", "application/json", "123456")]
    [InlineData("{\"pin\":123456}", "application/json", "123456")]
    [InlineData("pin=123456", "application/x-www-form-urlencoded", "123456")]
    [InlineData("{\"pin\":true}", "application/json", null)]
    [InlineData("{\"pin\":null}", "application/json", null)]
    [InlineData("{\"pin\":{}}", "application/json", null)]
    [InlineData("{\"pin\":[1]}", "application/json", null)]
    [InlineData("[]", "application/json", null)]
    [InlineData("\"123456\"", "application/json", null)]
    [InlineData("42", "application/json", null)]
    [InlineData("{", "application/json", null)]
    [InlineData("{}", null, null)]
    public void ParsePin_MalformedBodiesGiveNull(string body, string? contentType, string? expected)
    {
        // Null is a wrong PIN (a failed attempt), not an exception that bypasses the login guard
        Assert.Equal(expected, RemoteControlService.ParsePin(body, contentType));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectPrimaryActive_PrefersAudioUnderAnImage(bool audioFirst)
    {
        var image = CreateMedia(MediaClassification.Image, active: true, paused: false);
        var audio = CreateMedia(MediaClassification.Audio, active: true, paused: true);
        var idle = CreateMedia(MediaClassification.Video, active: false, paused: false);
        List<MediaItem> items = audioFirst ? [idle, audio, image] : [idle, image, audio];

        Assert.Same(audio, RemoteControlService.SelectPrimaryActive(items));
        Assert.Same(image, RemoteControlService.SelectNavigationAnchor(items));
    }

    [Fact]
    public void SelectPrimaryActive_OtherwiseFirstActive()
    {
        var slideshow = CreateSlideshow(0, 3, loop: false);
        var audio = CreateMedia(MediaClassification.Audio, active: true, paused: false);
        var idle = CreateMedia(MediaClassification.Image, active: false, paused: false);

        Assert.Same(slideshow, RemoteControlService.SelectPrimaryActive([idle, slideshow]));
        Assert.Same(audio, RemoteControlService.SelectNavigationAnchor([idle, audio]));
        Assert.Null(RemoteControlService.SelectPrimaryActive([idle]));
        Assert.Null(RemoteControlService.SelectNavigationAnchor([idle]));
    }

    [Fact]
    public void FindRelativeItem_StepsFromTheImageNotTheBackgroundAudio()
    {
        var audio = CreateMedia(MediaClassification.Audio, active: true, paused: false);
        var image = CreateMedia(MediaClassification.Image, active: true, paused: false);
        var next = CreateMedia(MediaClassification.Image, active: false, paused: false);
        audio.IsVisible = image.IsVisible = next.IsVisible = true;

        Assert.Equal(next.Id, RemoteControlService.FindRelativeItem([audio, image, next], 1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetItemsToStop_NewAudioStopsPlayingAudioButLeavesTheImage(bool imageFirst)
    {
        var image = CreateMedia(MediaClassification.Image, active: true, paused: false);
        var audio = CreateMedia(MediaClassification.Audio, active: true, paused: false);
        var newAudio = CreateMedia(MediaClassification.Audio, active: false, paused: false);
        List<MediaItem> items = imageFirst ? [image, audio, newAudio] : [audio, image, newAudio];

        Assert.Equal([audio.Id], RemoteControlService.GetItemsToStop(items, newAudio));
    }

    [Fact]
    public void GetItemsToStop_FollowsOperatorRules()
    {
        var image = CreateMedia(MediaClassification.Image, active: true, paused: false);
        var audio = CreateMedia(MediaClassification.Audio, active: true, paused: false);
        var video = CreateMedia(MediaClassification.Video, active: true, paused: false);
        var rolling = CreateSlideshow(0, 3, loop: false);
        rolling.IsRollingSlideshow = true;
        var newImage = CreateMedia(MediaClassification.Image, active: false, paused: false);
        var newVideo = CreateMedia(MediaClassification.Video, active: false, paused: false);
        var newAudio = CreateMedia(MediaClassification.Audio, active: false, paused: false);

        // An image replaces an image itself and plays over audio
        Assert.Empty(RemoteControlService.GetItemsToStop([image, audio, newImage], newImage));

        // Video stops audio; the image stays until the video replaces it
        Assert.Equal([audio.Id], RemoteControlService.GetItemsToStop([image, audio, newVideo], newVideo));

        // Nothing starts while a video plays
        Assert.Equal([video.Id], RemoteControlService.GetItemsToStop([video, newImage], newImage));

        // Only audio can start while a rolling slideshow runs
        Assert.Equal([rolling.Id], RemoteControlService.GetItemsToStop([rolling, newImage], newImage));
        Assert.Empty(RemoteControlService.GetItemsToStop([rolling, newAudio], newAudio));

        // Only audio can start while a web page or PDF is showing
        var web = CreateMedia(MediaClassification.Web, active: true, paused: false);
        var newWeb = CreateMedia(MediaClassification.Web, active: false, paused: false);
        Assert.Equal([web.Id], RemoteControlService.GetItemsToStop([web, newImage], newImage));
        Assert.Equal([web.Id], RemoteControlService.GetItemsToStop([web, newVideo], newVideo));
        Assert.Equal([web.Id], RemoteControlService.GetItemsToStop([web, newWeb], newWeb));
        Assert.Empty(RemoteControlService.GetItemsToStop([web, newAudio], newAudio));

        // The target itself is never stopped
        Assert.Empty(RemoteControlService.GetItemsToStop([audio], audio));
    }

    [Fact]
    public void BuildStatus_ListsAllActiveItems()
    {
        var image = CreateMedia(MediaClassification.Image, active: true, paused: false);
        var audio = CreateMedia(MediaClassification.Audio, active: true, paused: false);

        using var json = Parse(RemoteControlService.BuildStatus(audio, 50, volumeEnabled: true, [image.Id, audio.Id]));
        using var single = Parse(RemoteControlService.BuildStatus(audio, 50, volumeEnabled: true));
        using var none = Parse(RemoteControlService.BuildStatus(null, 50, volumeEnabled: true));

        Assert.Equal(audio.Id.ToString(), json.RootElement.GetProperty("currentItemId").GetString());
        Assert.Equal(25, json.RootElement.GetProperty("positionSeconds").GetDouble());
        Assert.Equal(
            [image.Id.ToString(), audio.Id.ToString()],
            json.RootElement.GetProperty("activeItemIds").EnumerateArray().Select(e => e.GetString()!).ToArray());
        Assert.Equal(1, single.RootElement.GetProperty("activeItemIds").GetArrayLength());
        Assert.Equal(0, none.RootElement.GetProperty("activeItemIds").GetArrayLength());
    }

    [Fact]
    public void FindRelativeItem_ReturnsNullWhenNothingIsVisible()
    {
        Assert.Null(RemoteControlService.FindRelativeItem([], 1));
        Assert.Null(RemoteControlService.FindRelativeItem([new MediaItem { Id = Guid.NewGuid(), IsVisible = false }], 1));
    }

    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "image/png")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, "image/gif")]
    [InlineData(new byte[] { 0x42, 0x4D, 0x00, 0x00 }, "image/bmp")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 }, "image/webp")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x41, 0x56, 0x45 }, "image/jpeg")] // RIFF but not WebP
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg")]
    [InlineData(new byte[] { 0x89 }, "image/jpeg")] // too short to be anything else
    public void GetImageContentType_FromSignature(byte[] bytes, string expected)
    {
        Assert.Equal(expected, RemoteControlService.GetImageContentType(bytes));
    }

    [Theory]
    [InlineData(null, false)] // no rule (or unreadable)
    [InlineData(NetworkFirewallInfo.FirewallProfilePrivate, false)]
    [InlineData(NetworkFirewallInfo.FirewallProfilePrivate | NetworkFirewallInfo.FirewallProfilePublic, true)]
    [InlineData(NetworkFirewallInfo.FirewallProfileDomain | NetworkFirewallInfo.FirewallProfilePrivate, false)]
    [InlineData(int.MaxValue, true)] // NET_FW_PROFILE2_ALL ("Any")
    public void NetworkFirewallInfo_CoversPublic(int? profiles, bool expected)
    {
        Assert.Equal(expected, NetworkFirewallInfo.CoversPublic(profiles));
    }

    private static JsonDocument Parse(object status) => JsonDocument.Parse(RemoteControlService.SerializeJson(status));
}
