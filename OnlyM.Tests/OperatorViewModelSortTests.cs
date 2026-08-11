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
///   - PrepareManualSortForDrag (including the _suppressSortModeReload guard)
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

        // When SortMode is set, update the backing field AND raise the event.
        // This lets the suppression guard test exercise the real code path.
        _optionsMock.SetupSet(x => x.SortMode = It.IsAny<MediaSortMode>())
            .Callback<MediaSortMode>(value =>
            {
                _currentSortMode = value;
                _optionsMock.Raise(x => x.SortModeChangedEvent += null, EventArgs.Empty);
            });

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

    // ── PrepareManualSortForDrag ───────────────────────────────────────────

    [Fact]
    public void PrepareManualSortForDrag_DoesNothing_WhenAlreadyManual()
    {
        _currentSortMode = MediaSortMode.Manual;

        _vm.PrepareManualSortForDrag();

        // SortMode setter should never have been called.
        _optionsMock.VerifySet(x => x.SortMode = It.IsAny<MediaSortMode>(), Times.Never);
        _dbMock.Verify(x => x.UpsertMediaOrder(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()), Times.Never);
    }

    [Fact]
    public void PrepareManualSortForDrag_SetsSortModeToManual_WhenAutoMode()
    {
        _currentSortMode = MediaSortMode.Auto;

        _vm.PrepareManualSortForDrag();

        Assert.Equal(MediaSortMode.Manual, _currentSortMode);
    }

    [Fact]
    public void PrepareManualSortForDrag_CallsUpsertWithCurrentScreenOrder_WhenAutoMode()
    {
        _currentSortMode = MediaSortMode.Auto;

        var item1 = MakeItem("a.jpg");
        var item2 = MakeItem("b.jpg");
        var item3 = MakeItem("c.jpg");
        _vm.MediaItems.Add(item1);
        _vm.MediaItems.Add(item2);
        _vm.MediaItems.Add(item3);

        _vm.PrepareManualSortForDrag();

        // UpsertMediaOrder must have been called once with the screen-order keys.
        _dbMock.Verify(x => x.UpsertMediaOrder(
            It.IsAny<string>(),
            It.Is<IReadOnlyList<string>>(keys =>
                keys.Count == 3 &&
                keys[0].Contains("a.jpg") &&
                keys[1].Contains("b.jpg") &&
                keys[2].Contains("c.jpg"))),
            Times.Once);
    }

    [Fact]
    public void PrepareManualSortForDrag_SuppressionPreventsLoadReload()
    {
        // The IOptionsService mock raises SortModeChangedEvent when SortMode is set.
        // If _suppressSortModeReload were missing, the handler would call
        // Application.Current.Dispatcher.BeginInvoke(...), which throws NRE in tests.
        // A clean execution here proves the guard works.
        _currentSortMode = MediaSortMode.Auto;

        var ex = Record.Exception(() => _vm.PrepareManualSortForDrag());

        Assert.Null(ex);
    }

    // ── MoveMediaItem ──────────────────────────────────────────────────────

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
    public void MoveMediaItem_PersistsNewOrderToDatabase()
    {
        _currentSortMode = MediaSortMode.Manual;

        var item1 = MakeItem("first.jpg");
        var item2 = MakeItem("second.jpg");
        _vm.MediaItems.Add(item1);
        _vm.MediaItems.Add(item2);

        _vm.MoveMediaItem(item2, item1);

        _dbMock.Verify(x => x.UpsertMediaOrder(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyList<string>>()),
            Times.AtLeastOnce);
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
