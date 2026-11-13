using Moq;
using OnlyM.Core.Services.CommandLine;
using OnlyM.Core.Services.Database;
using OnlyM.Core.Services.Media;
using OnlyM.Core.Services.Options;
using OnlyM.CoreSys.Services.Snackbar;
using OnlyM.Models;
using OnlyM.Services.Dialogs;
using OnlyM.Services.DragAndDrop;
using OnlyM.Services.FrozenVideoItems;
using OnlyM.Services.HiddenMediaItems;
using OnlyM.Services.MediaChanging;
using OnlyM.Services.Pages;
using OnlyM.Services.PdfOptions;
using OnlyM.ViewModel;

namespace OnlyM.Tests;

/// <summary>
/// Tests for the media list menu (top of the Operator page): Hide all, Unhide all,
/// Delete all / Delete hidden items, Open folder and Recent folders.
/// </summary>
[Collection(OperatorViewModelCollection.Name)]
public sealed class OperatorViewModelMediaListMenuTests : IDisposable
{
    private readonly Mock<IOptionsService> _optionsMock = new();
    private readonly Mock<IHiddenMediaItemsService> _hiddenMock = new();
    private readonly Mock<IActiveMediaItemsService> _activeMock = new();
    private readonly Mock<IDialogService> _dialogMock = new();
    private readonly Mock<ICommandLineService> _commandLineMock = new();
    private readonly Mock<IDatabaseService> _dbMock = new();

    private readonly string _mediaFolder = Directory.CreateTempSubdirectory("OnlyMMenuTests").FullName;
    private readonly string _otherFolder = Directory.CreateTempSubdirectory("OnlyMMenuTestsOther").FullName;
    private readonly OperatorViewModel _vm;

    public OperatorViewModelMediaListMenuTests()
    {
        _optionsMock.Setup(x => x.MediaFolder).Returns(_mediaFolder);
        _optionsMock.Setup(x => x.IncludeBlankScreenItem).Returns(false);
        _optionsMock.Setup(x => x.PermanentBackdrop).Returns(true);
        _optionsMock.Setup(x => x.MaxItemCount).Returns(100);
        _optionsMock.Setup(x => x.MetaDataParallelism).Returns(1);
        _optionsMock.Setup(x => x.RecentlyUsedMediaFolders).Returns([_mediaFolder, _otherFolder]);
        _dbMock.Setup(x => x.GetMediaOrderItemKeys(It.IsAny<string>())).Returns([]);

        _vm = new OperatorViewModel(
            new Mock<IMediaProviderService>().Object,
            new Mock<IThumbnailService>().Object,
            new Mock<IMediaMetaDataService>().Object,
            _dbMock.Object,
            _optionsMock.Object,
            new Mock<IPageService>().Object,
            new Mock<IFolderWatcherService>().Object,
            new Mock<IMediaStatusChangingService>().Object,
            _hiddenMock.Object,
            _activeMock.Object,
            new Mock<IFrozenVideosService>().Object,
            new Mock<IPdfOptionsService>().Object,
            new Mock<ISnackbarService>().Object,
            _dialogMock.Object,
            new Mock<IDragAndDropService>().Object,
            _commandLineMock.Object);
    }

    public void Dispose()
    {
        _vm.Dispose();
        Directory.Delete(_mediaFolder, recursive: true);
        Directory.Delete(_otherFolder, recursive: true);
    }

    [Fact]
    public void HideAll_HidesVisibleItems_ButNotActiveOrBlankScreenItems()
    {
        var normal = AddItem("a.jpg");
        var alreadyHidden = AddItem("b.jpg", visible: false);
        var active = AddItem("c.mp4", active: true);
        var blank = AddItem("blank.png", blank: true);

        _vm.HideAllCommand.Execute(null);

        Assert.False(normal.IsVisible);
        Assert.False(alreadyHidden.IsVisible);
        Assert.True(active.IsVisible);
        Assert.True(blank.IsVisible);
        _hiddenMock.Verify(x => x.Add(normal.FilePath!), Times.Once);
        _hiddenMock.Verify(x => x.Add(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void HideAll_IsDisabled_WhenNothingToHide()
    {
        AddItem("a.jpg", visible: false);
        AddItem("b.mp4", active: true);

        Assert.False(_vm.HideAllCommand.CanExecute(null));
    }

    [Fact]
    public void UnhideAll_DelegatesToHiddenItemsService()
    {
        _hiddenMock.Setup(x => x.SomeHiddenMediaItems()).Returns(true);

        Assert.True(_vm.UnhideAllCommand.CanExecute(null));
        _vm.UnhideAllCommand.Execute(null);

        _hiddenMock.Verify(x => x.UnhideAllMediaItems(), Times.Once);
    }

    [Fact]
    public void GetDeletableItems_IncludesHiddenItems_AndExcludesActiveAndBlankScreenItems()
    {
        var visible = AddItem("a.jpg");
        var hidden = AddItem("b.jpg", visible: false);
        AddItem("c.mp4", active: true);
        AddItem("d.mp4", visible: false, active: true);
        AddItem("blank.png", blank: true);

        Assert.Equal([visible, hidden], _vm.GetDeletableItems(hiddenOnly: false));
        Assert.Equal([hidden], _vm.GetDeletableItems(hiddenOnly: true));
    }

    [Fact]
    public async Task DeleteAll_WhenNotConfirmed_DeletesNothing()
    {
        var a = AddItem("a.jpg", createFile: true);
        var b = AddItem("b.jpg", visible: false, createFile: true);
        _dialogMock
            .Setup(x => x.ConfirmAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        await _vm.DeleteAllCommand.ExecuteAsync(null);

        _dialogMock.Verify(
            x => x.ConfirmAsync("Move all 2 media files to the Recycle Bin?", null, It.IsAny<string>()),
            Times.Once);
        Assert.True(File.Exists(a.FilePath));
        Assert.True(File.Exists(b.FilePath));
    }

    [Fact]
    public async Task DeleteHidden_ConfirmationMentionsSkippedActiveItems()
    {
        AddItem("a.jpg");
        AddItem("b.jpg", visible: false);
        AddItem("c.mp4", visible: false, active: true);
        _dialogMock
            .Setup(x => x.ConfirmAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        await _vm.DeleteHiddenCommand.ExecuteAsync(null);

        _dialogMock.Verify(
            x => x.ConfirmAsync(
                "Move the 1 hidden media files to the Recycle Bin?",
                "Items currently being shown will not be deleted.",
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public void DeleteCommands_AreDisabled_WhenNothingToDelete()
    {
        AddItem("a.jpg");

        Assert.True(_vm.DeleteAllCommand.CanExecute(null));
        Assert.False(_vm.DeleteHiddenCommand.CanExecute(null));
    }

    [Fact]
    public void OpenFolder_IsDisabled_WhenNoFolderCommandLineSwitchUsed()
    {
        Assert.True(_vm.OpenMediaFolderCommand.CanExecute(null));

        _commandLineMock.Setup(x => x.NoFolder).Returns(true);

        Assert.False(_vm.OpenMediaFolderCommand.CanExecute(null));
    }

    [Fact]
    public void RecentMediaFolders_MarksCurrentFolder()
    {
        var folders = _vm.RecentMediaFolders;

        Assert.Equal([_mediaFolder, _otherFolder], folders.Select(x => x.Path));
        Assert.True(folders[0].IsCurrent);
        Assert.False(folders[1].IsCurrent);
        Assert.True(folders[0].Command.CanExecute(_mediaFolder));
        Assert.True(folders[1].Command.CanExecute(_otherFolder));
    }

    [Fact]
    public void SelectingCurrentRecentFolder_DoesNothing()
    {
        _vm.SelectRecentFolderCommand.Execute(_mediaFolder);

        _optionsMock.VerifySet(x => x.MediaFolder = It.IsAny<string>(), Times.Never);
    }

    [Fact]
    public void RecentFolders_CannotBeSelected_WhileMediaIsActive()
    {
        _activeMock.Setup(x => x.Any()).Returns(true);

        Assert.False(_vm.SelectRecentFolderCommand.CanExecute(_otherFolder));
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void ShowRecentFolders_HiddenWhenFolderOrSettingsLockedOnCommandLine(
        bool commandLineFolder, bool noSettings, bool expected)
    {
        _optionsMock.Setup(x => x.IsCommandLineMediaFolderSpecified()).Returns(commandLineFolder);
        _commandLineMock.Setup(x => x.NoSettings).Returns(noSettings);

        Assert.Equal(expected, _vm.ShowRecentFolders);
    }

    private MediaItem AddItem(
        string fileName, bool visible = true, bool active = false, bool blank = false, bool createFile = false)
    {
        var path = Path.Combine(_mediaFolder, fileName);
        if (createFile)
        {
            File.WriteAllText(path, string.Empty);
        }

        var item = new MediaItem
        {
            Id = Guid.NewGuid(),
            FilePath = path,
            IsVisible = visible,
            IsMediaActive = active,
            IsBlankScreen = blank,
        };

        _vm.MediaItems.Add(item);
        return item;
    }
}
