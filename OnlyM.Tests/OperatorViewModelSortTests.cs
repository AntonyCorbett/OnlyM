using Moq;
using OnlyM.Core.Models;
using OnlyM.Core.Services.Database;
using OnlyM.Core.Services.Media;
using OnlyM.Core.Services.Options;
using OnlyM.Models;
using OnlyM.Services.Dialogs;
using OnlyM.Services.FrozenVideoItems;
using OnlyM.Services.HiddenMediaItems;
using OnlyM.Services.MediaChanging;
using OnlyM.Services.Pages;
using OnlyM.Services.PdfOptions;
using OnlyM.ViewModel;
using OnlyM.CoreSys.Services.Snackbar;

namespace OnlyM.Tests;

/// <summary>
/// Unit tests covering the manual sort mode helpers added to OperatorViewModel:
///   - IsManualSortMode
///   - MoveMediaItem
/// </summary>
public sealed class OperatorViewModelSortTests : IDisposable
{
    // ── Mocks ──────────────────────────────────────────────────────────────
    private readonly Mock<IDatabaseService> _dbMock = new();
    private readonly Mock<IOptionsService> _optionsMock = new();

    // Backing field so SortMode getter returns whatever was last set.
    private MediaSortMode _currentSortMode = MediaSortMode.Auto;

    // The system under test.
    private readonly OperatorViewModel _vm;

    // Temp folder used as the mock MediaFolder (always exists).
    private readonly string _mediaFolder = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);

    public OperatorViewModelSortTests()
    {
        // ── IOptionsService setup ──────────────────────────────────────────
        _optionsMock.SetupGet(x => x.SortMode).Returns(() => _currentSortMode);
        _optionsMock.Setup(x => x.MediaFolder).Returns(_mediaFolder);
        _optionsMock.Setup(x => x.IncludeBlankScreenItem).Returns(false);
        _optionsMock.Setup(x => x.PermanentBackdrop).Returns(true);
        _optionsMock.Setup(x => x.MaxItemCount).Returns(100);
        _optionsMock.Setup(x => x.MetaDataParallelism).Returns(1);

        // ── DB mock – allow any UpsertMediaOrder calls ────────────────────
        _dbMock.Setup(x => x.GetMediaOrderItemKeys(It.IsAny<string>()))
               .Returns([]);

        // ── Construct OperatorViewModel with all other deps mocked ─────────
        _vm = new OperatorViewModel(
            new Mock<IMediaProviderService>().Object,
            new Mock<IThumbnailService>().Object,
            new Mock<IMediaMetaDataService>().Object,
            _dbMock.Object,
            _optionsMock.Object,
            new Mock<IPageService>().Object,
            new Mock<IFolderWatcherService>().Object,
            new Mock<IMediaStatusChangingService>().Object,
            new Mock<IHiddenMediaItemsService>().Object,
            new Mock<IActiveMediaItemsService>().Object,
            new Mock<IFrozenVideosService>().Object,
            new Mock<IPdfOptionsService>().Object,
            new Mock<ISnackbarService>().Object,
            new Mock<IDialogService>().Object);
    }

    public void Dispose() => _vm.Dispose();

    // ── Helpers ────────────────────────────────────────────────────────────

    private MediaItem MakeItem(string fileName) => new()
    {
        Id = Guid.NewGuid(),
        FilePath = Path.Combine(_mediaFolder, fileName),
        IsVisible = true,
    };

    // ── IsManualSortMode ───────────────────────────────────────────────────

    [Fact]
    public void IsManualSortMode_ReturnsFalse_WhenSortModeIsAuto()
    {
        _currentSortMode = MediaSortMode.Auto;

        Assert.False(_vm.IsManualSortMode);
    }

    [Fact]
    public void IsManualSortMode_ReturnsTrue_WhenSortModeIsManual()
    {
        _currentSortMode = MediaSortMode.Manual;

        Assert.True(_vm.IsManualSortMode);
    }

    // ── MoveMediaItem ──────────────────────────────────────────────────────

    [Fact]
    public void MoveMediaItem_DoesNothing_WhenSortModeIsAuto()
    {
        _currentSortMode = MediaSortMode.Auto;

        var item1 = MakeItem("1.jpg");
        var item2 = MakeItem("2.jpg");
        _vm.MediaItems.Add(item1);
        _vm.MediaItems.Add(item2);

        _vm.MoveMediaItem(item2, item1);

        // Order must be unchanged, and nothing persisted.
        Assert.Equal(item1, _vm.MediaItems[0]);
        Assert.Equal(item2, _vm.MediaItems[1]);
        _dbMock.Verify(x => x.UpsertMediaOrder(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()), Times.Never);
    }

    [Fact]
    public void MoveMediaItem_MovesSourceToTargetPosition()
    {
        _currentSortMode = MediaSortMode.Manual;

        var item1 = MakeItem("1.jpg");
        var item2 = MakeItem("2.jpg");
        var item3 = MakeItem("3.jpg");
        _vm.MediaItems.Add(item1);
        _vm.MediaItems.Add(item2);
        _vm.MediaItems.Add(item3);

        // Move item3 (index 2) to item1's position (index 0).
        _vm.MoveMediaItem(item3, item1);

        Assert.Equal(item3, _vm.MediaItems[0]);
        Assert.Equal(item1, _vm.MediaItems[1]);
        Assert.Equal(item2, _vm.MediaItems[2]);
    }

    [Fact]
    public async Task MoveMediaItem_PersistsNewOrderToDatabase()
    {
        _currentSortMode = MediaSortMode.Manual;

        var persisted = new TaskCompletionSource<(string ScopeKey, string[] ItemKeys)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _dbMock.Setup(x => x.UpsertMediaOrder(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()))
            .Callback<string, IReadOnlyList<string>>((scopeKey, itemKeys) =>
                persisted.TrySetResult((scopeKey, itemKeys.ToArray())));

        var item1 = MakeItem("first.jpg");
        var item2 = MakeItem("second.jpg");
        _vm.MediaItems.Add(item1);
        _vm.MediaItems.Add(item2);

        _vm.MoveMediaItem(item2, item1);

        var savedOrder = await persisted.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(_mediaFolder, savedOrder.ScopeKey);
        Assert.Equal(["second.jpg", "first.jpg"], savedOrder.ItemKeys);
    }

    [Fact]
    public void MoveMediaItem_DoesNothing_WhenSourceIsBlankScreen()
    {
        _currentSortMode = MediaSortMode.Manual;

        var blank = new MediaItem
        {
            Id = Guid.NewGuid(),
            FilePath = Path.Combine(_mediaFolder, "blank.png"),
            IsVisible = true,
            IsBlankScreen = true,
        };
        var normal = MakeItem("normal.jpg");
        _vm.MediaItems.Add(blank);
        _vm.MediaItems.Add(normal);

        _vm.MoveMediaItem(blank, normal);

        // Order must be unchanged.
        Assert.Equal(blank, _vm.MediaItems[0]);
        Assert.Equal(normal, _vm.MediaItems[1]);
    }

    [Fact]
    public void MoveMediaItem_DoesNothing_WhenTargetIsBlankScreen()
    {
        _currentSortMode = MediaSortMode.Manual;

        var blank = new MediaItem
        {
            Id = Guid.NewGuid(),
            FilePath = Path.Combine(_mediaFolder, "blank.png"),
            IsVisible = true,
            IsBlankScreen = true,
        };
        var normal = MakeItem("normal.jpg");
        _vm.MediaItems.Add(blank);
        _vm.MediaItems.Add(normal);

        _vm.MoveMediaItem(normal, blank);

        // Order must be unchanged.
        Assert.Equal(blank, _vm.MediaItems[0]);
        Assert.Equal(normal, _vm.MediaItems[1]);
    }
}
