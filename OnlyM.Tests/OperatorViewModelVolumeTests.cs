using MaterialDesignThemes.Wpf;
using Moq;
using OnlyM.Core.Services.Database;
using OnlyM.Core.Services.Media;
using OnlyM.Core.Services.Options;
using OnlyM.CoreSys.Services.Snackbar;
using OnlyM.Services.Dialogs;
using OnlyM.Services.FrozenVideoItems;
using OnlyM.Services.HiddenMediaItems;
using OnlyM.Services.MediaChanging;
using OnlyM.Services.Pages;
using OnlyM.Services.PdfOptions;
using OnlyM.ViewModel;

namespace OnlyM.Tests;

/// <summary>
/// Tests for the operator toolbar volume control state in OperatorViewModel:
/// mute toggle / restore, icon thresholds, tooltips and sync with changes made elsewhere
/// (e.g. the HTTP remote, which sets IOptionsService.Volume directly).
/// </summary>
[Collection(OperatorViewModelCollection.Name)]
public sealed class OperatorViewModelVolumeTests : IDisposable
{
    private readonly Mock<IOptionsService> _optionsMock = new();
    private readonly List<OperatorViewModel> _created = [];
    private DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private int _volume = 100;

    public OperatorViewModelVolumeTests()
    {
        _optionsMock.SetupGet(x => x.Volume).Returns(() => _volume);
        _optionsMock.SetupSet(x => x.Volume = It.IsAny<int>())
            .Callback<int>(value =>
            {
                // Mirrors OptionsService: clamp to 0-100 and raise the event only on a change
                var clamped = Math.Clamp(value, 0, 100);
                if (clamped != _volume)
                {
                    _volume = clamped;
                    _optionsMock.Raise(x => x.VolumeChangedEvent += null, EventArgs.Empty);
                }
            });
        _optionsMock.Setup(x => x.MediaFolder).Returns(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar));
        _optionsMock.Setup(x => x.MaxItemCount).Returns(100);
        _optionsMock.Setup(x => x.MetaDataParallelism).Returns(1);
    }

    public void Dispose()
    {
        foreach (var vm in _created)
        {
            vm.Dispose();
        }
    }

    [Theory]
    [InlineData(0, PackIconKind.VolumeOff)]
    [InlineData(1, PackIconKind.VolumeLow)]
    [InlineData(33, PackIconKind.VolumeLow)]
    [InlineData(34, PackIconKind.VolumeMedium)]
    [InlineData(66, PackIconKind.VolumeMedium)]
    [InlineData(67, PackIconKind.VolumeHigh)]
    [InlineData(100, PackIconKind.VolumeHigh)]
    public void VolumeIconKind_FollowsThresholds(int volume, PackIconKind expected)
    {
        var vm = CreateVm(volume);

        Assert.Equal(expected, vm.VolumeIconKind);
        Assert.Equal(expected, OperatorViewModel.GetVolumeIconKind(volume));
    }

    [Theory]
    [InlineData(100, "Volume 100%", "100%", false)]
    [InlineData(99, "Volume 99% - media will play quieter", "99%", false)]
    [InlineData(65, "Volume 65% - media will play quieter", "65%", false)]
    [InlineData(1, "Volume 1% - media will play quieter", "1%", false)]
    [InlineData(0, "Muted", "0%", true)]
    public void ToolTipAndLabel_ReflectVolume(int volume, string toolTip, string label, bool muted)
    {
        var vm = CreateVm(volume);

        Assert.Equal(toolTip, vm.VolumeToolTip);
        Assert.Equal(label, vm.VolumeLabel);
        Assert.Equal(label, OperatorViewModel.GetVolumeLabel(volume));
        Assert.Equal(muted, vm.IsMuted);
        Assert.StartsWith(toolTip, vm.MuteButtonToolTip, StringComparison.Ordinal);
    }

    [Fact]
    public void ToggleMute_MutesThenRestoresPreviousLevel()
    {
        var vm = CreateVm(60);
        Assert.Equal("Mute", vm.MuteButtonName);

        vm.ToggleMuteCommand.Execute(null);

        Assert.Equal(0, _volume);
        Assert.True(vm.IsMuted);
        Assert.Equal(PackIconKind.VolumeOff, vm.VolumeIconKind);
        Assert.Equal("Unmute", vm.MuteButtonName);
        Assert.EndsWith("Click to unmute (restores 60%)", vm.MuteButtonToolTip, StringComparison.Ordinal);

        _now += TimeSpan.FromSeconds(10);
        vm.ToggleMuteCommand.Execute(null);

        Assert.Equal(60, _volume);
        Assert.False(vm.IsMuted);
        Assert.Equal("Mute", vm.MuteButtonName);
    }

    [Fact]
    public void ToggleMute_ImmediatelyAfterAChange_RestoresTheNewLevel()
    {
        var vm = CreateVm(60);
        _now += TimeSpan.FromSeconds(5);

        vm.Volume = 80;
        _now += TimeSpan.FromMilliseconds(100);
        vm.ToggleMuteCommand.Execute(null);
        vm.ToggleMuteCommand.Execute(null);

        Assert.Equal(80, _volume);
    }

    [Fact]
    public void DraggingSliderDownToZero_UnmuteRestoresLevelBeforeTheDrag()
    {
        var vm = CreateVm(60);
        _now += TimeSpan.FromSeconds(30);

        for (var v = 59; v >= 0; v--)
        {
            vm.Volume = v;
            _now += TimeSpan.FromMilliseconds(15);
        }

        Assert.True(vm.IsMuted);
        Assert.Equal(60, vm.RestoreVolume);

        vm.ToggleMuteCommand.Execute(null);

        Assert.Equal(60, _volume);
    }

    [Fact]
    public void SettledLevel_IsWhatUnmuteRestores()
    {
        var vm = CreateVm(100);
        _now += TimeSpan.FromSeconds(5);
        vm.Volume = 40;
        _now += TimeSpan.FromSeconds(5);

        // Muted remotely (HTTP API sets the option directly)
        _optionsMock.Object.Volume = 0;
        Assert.True(vm.IsMuted);

        vm.ToggleMuteCommand.Execute(null);

        Assert.Equal(40, _volume);
    }

    [Fact]
    public void RemoteSetAboveZero_UnmutesAndNotifiesBindings()
    {
        var vm = CreateVm(60);
        vm.ToggleMuteCommand.Execute(null);
        Assert.True(vm.IsMuted);

        var notifications = new List<string?>();
        vm.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);

        _now += TimeSpan.FromSeconds(5);
        _optionsMock.Object.Volume = 30;

        Assert.False(vm.IsMuted);
        Assert.Equal(30, vm.Volume);
        Assert.Equal(PackIconKind.VolumeLow, vm.VolumeIconKind);
        Assert.Equal("30%", vm.VolumeLabel);
        foreach (var name in new[]
                 {
                     nameof(OperatorViewModel.Volume),
                     nameof(OperatorViewModel.IsMuted),
                     nameof(OperatorViewModel.VolumeLabel),
                     nameof(OperatorViewModel.VolumeIconKind),
                     nameof(OperatorViewModel.VolumeToolTip),
                     nameof(OperatorViewModel.MuteButtonName),
                     nameof(OperatorViewModel.MuteButtonToolTip),
                 })
        {
            Assert.Contains(name, notifications);
        }

        // Mute / unmute now works from the remotely-set level
        _now += TimeSpan.FromSeconds(5);
        vm.ToggleMuteCommand.Execute(null);
        vm.ToggleMuteCommand.Execute(null);
        Assert.Equal(30, _volume);
    }

    [Fact]
    public void ShowVolumeControl_FollowsOptionAndNotifies()
    {
        var show = false;
        _optionsMock.SetupGet(x => x.ShowVolumeControl).Returns(() => show);
        var vm = CreateVm(100);
        Assert.False(vm.ShowVolumeControl);

        var notifications = new List<string?>();
        vm.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);

        show = true;
        _optionsMock.Raise(x => x.ShowVolumeControlChangedEvent += null, EventArgs.Empty);

        Assert.True(vm.ShowVolumeControl);
        Assert.Contains(nameof(OperatorViewModel.ShowVolumeControl), notifications);
    }

    [Fact]
    public void StartedWhileMuted_UnmuteRestoresDefaultLevel()
    {
        var vm = CreateVm(0);

        Assert.True(vm.IsMuted);
        vm.ToggleMuteCommand.Execute(null);

        Assert.Equal(OperatorViewModel.DefaultRestoreVolume, _volume);
    }

    private OperatorViewModel CreateVm(int initialVolume)
    {
        _volume = initialVolume;
        var vm = new OperatorViewModel(
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
            new Mock<IDialogService>().Object)
        {
            UtcNow = () => _now,
        };

        _created.Add(vm);
        return vm;
    }
}
