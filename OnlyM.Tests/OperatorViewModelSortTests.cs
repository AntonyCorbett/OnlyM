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

    [Theory]
    [InlineData("dated-folder/second.jpg")]
    [InlineData("beyond-item-limit.jpg")]
    public void SortMediaItems_RestoresSavedPositionsWhenExcludedItemsReturn(string excludedKey)
    {
        _currentSortMode = MediaSortMode.Manual;
        string[] savedOrder = ["third.jpg", excludedKey, "first.jpg"];
        _dbMock.Setup(x => x.GetMediaOrderItemKeys(_mediaFolder)).Returns(savedOrder);

        var first = MakeItem("first.jpg");
        var third = MakeItem("third.jpg");
        _vm.MediaItems.Add(first);
        _vm.MediaItems.Add(third);
        _vm.SortMediaItems();
        Assert.Equal([third, first], _vm.MediaItems);

        // A different date may even have no current items.
        _vm.MediaItems.Clear();
        _vm.SortMediaItems();

        var returning = MakeItem(excludedKey);
        _vm.MediaItems.Add(first);
        _vm.MediaItems.Add(third);
        _vm.MediaItems.Add(returning);
        _vm.SortMediaItems();

        Assert.Equal([third, returning, first], _vm.MediaItems);
        _dbMock.Verify(x => x.RemoveMissingMediaOrderItems(
            It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>()), Times.Never);
        _dbMock.Verify(x => x.UpsertMediaOrder(
            It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()), Times.Never);
    }

    [Theory]
    [InlineData("first.jpg,dated/hidden.jpg,second.jpg", "first.jpg,second.jpg", "second.jpg,dated/hidden.jpg,first.jpg")]
    [InlineData("hidden.jpg,first.jpg,other-hidden.jpg,second.jpg,tail.jpg", "first.jpg,second.jpg", "hidden.jpg,second.jpg,other-hidden.jpg,first.jpg,tail.jpg")]
    [InlineData("FIRST.JPG,hidden.jpg,SECOND.JPG", "first.jpg,second.jpg", "second.jpg,hidden.jpg,first.jpg")]
    [InlineData("first.jpg,hidden.jpg,second.jpg", "first.jpg,second.jpg,new.jpg", "new.jpg,hidden.jpg,first.jpg,second.jpg")]
    [InlineData("hidden.jpg", "first.jpg,second.jpg", "hidden.jpg,second.jpg,first.jpg")]
    public async Task MoveMediaItem_PreservesExcludedSlotsWhenSavingSubset(
        string storedKeys, string currentKeys, string expectedKeys)
    {
        _currentSortMode = MediaSortMode.Manual;
        var savedOrder = storedKeys.Split(',');
        var persisted = new TaskCompletionSource<string[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        _dbMock.Setup(x => x.GetMediaOrderItemKeys(_mediaFolder)).Returns(() => savedOrder);
        _dbMock.Setup(x => x.UpsertMediaOrder(_mediaFolder, It.IsAny<IReadOnlyList<string>>()))
            .Callback<string, IReadOnlyList<string>>((_, keys) =>
            {
                savedOrder = keys.ToArray();
                persisted.TrySetResult(savedOrder);
            });

        foreach (var key in currentKeys.Split(','))
        {
            _vm.MediaItems.Add(MakeItem(key));
        }

        _vm.MoveMediaItem(_vm.MediaItems[^1], _vm.MediaItems[0]);

        var result = await persisted.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(expectedKeys.Split(','), result);

        // Reload the full list in a different order and check the persisted result.
        _vm.MediaItems.Clear();
        foreach (var key in result.Reverse())
        {
            _vm.MediaItems.Add(MakeItem(key));
        }

        _vm.SortMediaItems();
        Assert.Equal(result.Select(key => Path.Combine(_mediaFolder, key)), _vm.MediaItems.Select(x => x.FilePath));
    }

    [Fact]
    public async Task MoveMediaItem_ConsecutiveSavesPreserveBothSubsets()
    {
        _currentSortMode = MediaSortMode.Manual;
        string[] savedOrder = ["first.jpg", "other-date/a.jpg", "second.jpg", "other-date/b.jpg"];
        var savedOrders = new List<string[]>();
        var persisted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _dbMock.Setup(x => x.GetMediaOrderItemKeys(_mediaFolder)).Returns(() => savedOrder);
        _dbMock.Setup(x => x.UpsertMediaOrder(_mediaFolder, It.IsAny<IReadOnlyList<string>>()))
            .Callback<string, IReadOnlyList<string>>((_, keys) =>
            {
                savedOrder = keys.ToArray();
                savedOrders.Add(savedOrder);
                if (savedOrders.Count == 2)
                {
                    persisted.TrySetResult();
                }
            });

        _vm.MediaItems.Add(MakeItem("first.jpg"));
        _vm.MediaItems.Add(MakeItem("second.jpg"));
        _vm.MoveMediaItem(_vm.MediaItems[1], _vm.MediaItems[0]);

        _vm.MediaItems.Clear();
        _vm.MediaItems.Add(MakeItem("other-date/a.jpg"));
        _vm.MediaItems.Add(MakeItem("other-date/b.jpg"));
        _vm.MoveMediaItem(_vm.MediaItems[1], _vm.MediaItems[0]);

        await persisted.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(["second.jpg", "other-date/a.jpg", "first.jpg", "other-date/b.jpg"], savedOrders[0]);
        Assert.Equal(["second.jpg", "other-date/b.jpg", "first.jpg", "other-date/a.jpg"], savedOrders[1]);
    }

    [Fact]
    public void SortMediaItems_AutoModeDoesNotReadOrModifyManualOrder()
    {
        var second = MakeItem("second.jpg");
        var first = MakeItem("first.jpg");
        _vm.MediaItems.Add(second);
        _vm.MediaItems.Add(first);

        _vm.SortMediaItems();

        Assert.Equal([first, second], _vm.MediaItems);
        _dbMock.VerifyNoOtherCalls();
    }
}
