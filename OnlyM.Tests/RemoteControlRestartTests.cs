using Moq;
using OnlyM.Core.Services.Database;
using OnlyM.Core.Services.Media;
using OnlyM.Core.Services.Monitors;
using OnlyM.Core.Services.Options;
using OnlyM.CoreSys.Services.Snackbar;
using OnlyM.Services.Dialogs;
using OnlyM.Services.FrozenVideoItems;
using OnlyM.Services.HiddenMediaItems;
using OnlyM.Services.MediaChanging;
using OnlyM.Services.Pages;
using OnlyM.Services.PdfOptions;
using OnlyM.Services.RemoteControl;
using OnlyM.ViewModel;

namespace OnlyM.Tests;

/// <summary>
/// Only the port, the mode and the enabled state may restart the remote control listener (and with it the Cloudflare
/// tunnel, which gets a new URL). Display options such as the remote volume control must apply live.
/// </summary>
public sealed class RemoteControlRestartTests : IDisposable
{
    private readonly Mock<IOptionsService> _optionsMock = new();
    private readonly Mock<IRemoteControlService> _remoteMock = new();
    private readonly OperatorViewModel _operatorViewModel;
    private bool _showRemoteVolume;
    private bool _showVolume;
    private int _port = 8089;

    public RemoteControlRestartTests()
    {
        _optionsMock.Setup(x => x.MediaFolder).Returns(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar));
        _optionsMock.Setup(x => x.MaxItemCount).Returns(100);
        _optionsMock.Setup(x => x.MetaDataParallelism).Returns(1);
        _optionsMock.SetupGet(x => x.RecentlyUsedMediaFolders).Returns([]);
        _optionsMock.SetupGet(x => x.Volume).Returns(100);
        _optionsMock.SetupGet(x => x.RemoteControlEnabled).Returns(true);
        _optionsMock.SetupGet(x => x.RemoteControlMode).Returns(RemoteControlMode.Cloudflare);
        _optionsMock.SetupGet(x => x.ShowRemoteVolumeControl).Returns(() => _showRemoteVolume);
        _optionsMock.SetupSet(x => x.ShowRemoteVolumeControl = It.IsAny<bool>()).Callback<bool>(v => _showRemoteVolume = v);
        _optionsMock.SetupGet(x => x.ShowVolumeControl).Returns(() => _showVolume);
        _optionsMock.SetupSet(x => x.ShowVolumeControl = It.IsAny<bool>()).Callback<bool>(v => _showVolume = v);
        _optionsMock.SetupGet(x => x.RemoteControlPort).Returns(() => _port);
        _optionsMock.SetupSet(x => x.RemoteControlPort = It.IsAny<int>()).Callback<int>(v => _port = v);

        _operatorViewModel = new OperatorViewModel(
            new Mock<IMediaProviderService>().Object,
            new Mock<IThumbnailService>().Object,
            new Mock<IMediaMetaDataService>().Object,
            new Mock<IDatabaseService>().Object,
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

    public void Dispose() => _operatorViewModel.Dispose();

    [Fact]
    public void SettingsPage_DisplayOptions_DoNotRestartRemoteControl()
    {
        var settings = CreateSettings();

        settings.ShowRemoteVolumeControl = true;
        settings.ShowRemoteVolumeControl = false;
        settings.ShowVolumeControl = true;
        settings.ShowVolumeControl = false;

        Assert.False(_showRemoteVolume);
        _remoteMock.Verify(x => x.Restart(), Times.Never);
        _remoteMock.Verify(x => x.ApplySettings(), Times.Never);
        _remoteMock.Verify(x => x.RetryTunnel(), Times.Never);
    }

    [Fact]
    public async Task SettingsPage_PortChange_RestartsRemoteControl()
    {
        var restarted = new TaskCompletionSource();
        _remoteMock.Setup(x => x.Restart()).Callback(() => restarted.TrySetResult());
        var settings = CreateSettings();

        settings.RemoteControlPort = "8090";

        await restarted.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(8090, _port);
        _remoteMock.Verify(x => x.Restart(), Times.Once);
    }

    [Fact]
    public void Service_DisplayOptionEvents_DoNotReapplySettings()
    {
        // Remote control disabled, so re-applying settings is harmless here, but it still raises StateChanged:
        // that is how a re-apply (which may restart the tunnel when enabled) would show up.
        _optionsMock.SetupGet(x => x.RemoteControlEnabled).Returns(false);
        using var service = new RemoteControlService(_operatorViewModel, new Mock<IThumbnailService>().Object, _optionsMock.Object);
        var applied = 0;
        service.StateChanged += (_, _) => applied++;

        _optionsMock.Raise(x => x.ShowRemoteVolumeControlChangedEvent += null, EventArgs.Empty);
        _optionsMock.Raise(x => x.ShowVolumeControlChangedEvent += null, EventArgs.Empty);
        _optionsMock.Raise(x => x.VolumeChangedEvent += null, EventArgs.Empty);
        _optionsMock.Raise(x => x.AllowVideoPositionSeekingChangedEvent += null, EventArgs.Empty);
        _optionsMock.Raise(x => x.RemoteControlPinChangedEvent += null, EventArgs.Empty);
        Assert.Equal(0, applied);
        Assert.False(service.IsRunning);

        _optionsMock.Raise(x => x.RemoteControlChangedEvent += null, EventArgs.Empty);
        Assert.Equal(1, applied);
    }

    private SettingsViewModel CreateSettings() => new(
        new Mock<IPageService>().Object,
        new Mock<IMonitorsService>().Object,
        _optionsMock.Object,
        new Mock<IActiveMediaItemsService>().Object,
        new Mock<IThumbnailService>().Object,
        new Mock<ISnackbarService>().Object,
        _remoteMock.Object);
}
