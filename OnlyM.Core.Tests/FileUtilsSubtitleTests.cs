using OnlyM.Core.Utils;

namespace OnlyM.Core.Tests;

public sealed class FileUtilsSubtitleTests : IDisposable
{
    private static readonly string[] VideoExtensions = [".mp4", ".mov", ".mkv"];

    private readonly string _folder = Directory.CreateTempSubdirectory("OnlyMSubtitles").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void ReturnsSubtitleFile_WhenVideoHasBeenDeleted()
    {
        var srt = Touch("talk.srt");

        var result = FileUtils.GetOrphanedSubtitleFilePath(Path.Combine(_folder, "talk.mp4"), VideoExtensions);

        Assert.Equal(srt, result);
    }

    [Fact]
    public void ReturnsNull_WhenThereIsNoSubtitleFile()
    {
        Touch("other.srt");

        var result = FileUtils.GetOrphanedSubtitleFilePath(Path.Combine(_folder, "talk.mp4"), VideoExtensions);

        Assert.Null(result);
    }

    [Fact]
    public void ReturnsNull_WhenAnotherVideoSharesTheBaseName()
    {
        Touch("talk.srt");
        Touch("talk.mov");

        var result = FileUtils.GetOrphanedSubtitleFilePath(Path.Combine(_folder, "talk.mp4"), VideoExtensions);

        Assert.Null(result);
    }

    [Fact]
    public void IgnoresNonVideoFilesWithTheSameBaseName()
    {
        var srt = Touch("talk.srt");
        Touch("talk.jpg");

        var result = FileUtils.GetOrphanedSubtitleFilePath(Path.Combine(_folder, "talk.mp4"), VideoExtensions);

        Assert.Equal(srt, result);
    }

    private string Touch(string fileName)
    {
        var path = Path.Combine(_folder, fileName);
        File.WriteAllText(path, string.Empty);
        return path;
    }
}
