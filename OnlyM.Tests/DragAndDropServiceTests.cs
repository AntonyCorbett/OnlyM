using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using Moq;
using OnlyM.Core.Services.Media;
using OnlyM.Core.Services.Options;
using OnlyM.CoreSys.Services.Snackbar;
using OnlyM.Models;
using OnlyM.PubSubMessages;
using OnlyM.Services.DragAndDrop;

namespace OnlyM.Tests;

public sealed class DragAndDropServiceTests : IDisposable
{
    private readonly string _testFolder = Path.Combine(Path.GetTempPath(), $"OnlyM-drop-{Guid.NewGuid()}");
    private readonly Mock<IOptionsService> _options = new();
    private readonly WeakReferenceMessenger _messenger = new();
    private readonly List<ExternalDropCompletedMessage> _completions = [];
    private readonly DragAndDropService _service;
    private readonly string _sourceFolder;
    private readonly string _destinationFolder;

    public DragAndDropServiceTests()
    {
        _sourceFolder = Directory.CreateDirectory(Path.Combine(_testFolder, "source")).FullName;
        _destinationFolder = Directory.CreateDirectory(Path.Combine(_testFolder, "destination")).FullName;
        _options.SetupGet(x => x.MediaFolder).Returns(_destinationFolder);
        var provider = new Mock<IMediaProviderService>();
        provider.Setup(x => x.IsFileExtensionSupported(".jpg")).Returns(true);
        _service = new DragAndDropService(provider.Object, _options.Object, new Mock<ISnackbarService>().Object, _messenger);
        _messenger.Register<ExternalDropCompletedMessage>(this, (_, message) => _completions.Add(message));
    }

    public void Dispose()
    {
        _messenger.UnregisterAll(this);
        Directory.Delete(_testFolder, recursive: true);
    }

    [Fact]
    public async Task CopyAsync_ReportsOnlyCopiedFilesAndOriginalDestination()
    {
        var first = CreateSource("first.jpg");
        var second = CreateSource("second.jpg");
        var existing = CreateSource("existing.jpg");
        var unsupported = CreateSource("unsupported.txt");
        File.WriteAllText(Path.Combine(_destinationFolder, "existing.jpg"), "keep original");
        var targetPath = Path.Combine(_destinationFolder, "target.jpg");
        _service.CopyingFilesProgressEvent += (_, args) =>
        {
            if (args.Status == FileCopyStatus.StartingCopy)
            {
                _options.SetupGet(x => x.MediaFolder).Returns(Path.Combine(_testFolder, "another-folder"));
            }
        };

        await _service.CopyAsync(FileData(second, existing, unsupported, first), 3, targetPath)
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        var completion = Assert.Single(_completions);
        Assert.Equal(_destinationFolder, completion.MediaFolder);
        Assert.Equal(3, completion.TargetIndex);
        Assert.Equal(targetPath, completion.TargetFilePath);
        Assert.Equal(
            [Path.Combine(_destinationFolder, "first.jpg"), Path.Combine(_destinationFolder, "second.jpg")],
            completion.CopiedFilePaths);
        Assert.All(completion.CopiedFilePaths, path => Assert.Equal("new media", File.ReadAllText(path)));
        Assert.Equal("keep original", File.ReadAllText(Path.Combine(_destinationFolder, "existing.jpg")));
    }

    [Fact]
    public async Task CopyAsync_UntargetedCopyDoesNotRequestManualInsertion()
    {
        await _service.CopyAsync(FileData(CreateSource("paste.jpg")))
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(_destinationFolder, "paste.jpg")));
        Assert.Empty(_completions);
    }

    [Theory]
    [InlineData("existing.jpg")]
    [InlineData("unsupported.txt")]
    public async Task CopyAsync_NoFilesCopiedDoesNotRequestManualInsertion(string fileName)
    {
        var source = CreateSource(fileName);
        File.WriteAllText(Path.Combine(_destinationFolder, fileName), "keep original");

        await _service.CopyAsync(FileData(source), 0)
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Empty(_completions);
        Assert.Equal("keep original", File.ReadAllText(Path.Combine(_destinationFolder, fileName)));
    }

    private static IDataObject FileData(params string[] paths)
    {
        var data = new Mock<IDataObject>();
        data.Setup(x => x.GetDataPresent(DataFormats.FileDrop)).Returns(true);
        data.Setup(x => x.GetData(DataFormats.FileDrop)).Returns(paths);
        return data.Object;
    }

    private string CreateSource(string fileName)
    {
        var path = Path.Combine(_sourceFolder, fileName);
        File.WriteAllText(path, "new media");
        return path;
    }
}
