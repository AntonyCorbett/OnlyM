using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.WindowsAPICodePack.Dialogs;
using OnlyM.AutoUpdates;
using OnlyM.Core.Extensions;
using OnlyM.Core.Models;
using OnlyM.Core.Services.Media;
using OnlyM.Core.Services.Monitors;
using OnlyM.Core.Services.Options;
using OnlyM.CoreSys.Services.Snackbar;
using OnlyM.Models;
using OnlyM.PubSubMessages;
using OnlyM.Services;
using OnlyM.Services.MediaChanging;
using OnlyM.Services.Pages;
using OnlyM.Services.RemoteControl;
using Serilog.Events;

using Size = System.Windows.Size;

namespace OnlyM.ViewModel;

// ReSharper disable once UnusedMember.Global
internal sealed class SettingsViewModel : ObservableObject
{
    private static readonly Size Size360P = new(640, 360);
    private static readonly Size Size480P = new(854, 480);
    private static readonly Size Size720P = new(1280, 720);
    private static readonly Size Size1080P = new(1920, 1080);

    private static readonly Size MinSize = new(192, 108);
    private static readonly Size MaxSize = new(8192, 6144);

    private readonly IPageService _pageService;
    private readonly IMonitorsService _monitorsService;
    private readonly IOptionsService _optionsService;
    private readonly IThumbnailService _thumbnailService;
    private readonly IActiveMediaItemsService _activeMediaItemsService;
    private readonly ISnackbarService _snackbarService;
    private readonly MonitorItem[] _monitors;
    private readonly MirrorHotKeyItem[] _mirrorHotKeys;
    private readonly LanguageItem[] _languages;
    private readonly RenderingMethodItem[] _renderingMethods;
    private readonly LoggingLevel[] _loggingLevels;
    private readonly ImageFade[] _fadingTypes;
    private readonly ImageFadeSpeed[] _fadingSpeeds;
    private readonly RecentlyUsedFolders _recentlyUsedMediaFolders;
    private readonly MagnifierShapeItem[] _magnifierShapes;
    private readonly MagnifierSizeItem[] _magnifierSizes;
    private readonly AppModeItem[] _appModes;
    private readonly IRemoteControlService _remoteControlService;
    private readonly RemoteControlModeItem[] _remoteControlModes =
    [
        new(RemoteControlMode.Local, Properties.Resources.REMOTE_CONTROL_MODE_LOCAL),
        new(RemoteControlMode.Cloudflare, Properties.Resources.REMOTE_CONTROL_MODE_CLOUDFLARE),
    ];

    private bool _installingCloudflared;
    private bool _cloudflaredInstallFailed;
    private string? _qrCodeUrl;
    private System.Windows.Media.Imaging.BitmapImage? _remoteControlQrCode;
    private int? _previousRemoteControlPort;

    public SettingsViewModel(
        IPageService pageService,
        IMonitorsService monitorsService,
        IOptionsService optionsService,
        IActiveMediaItemsService activeMediaItemsService,
        IThumbnailService thumbnailService,
        ISnackbarService snackbarService,
        IRemoteControlService remoteControlService)
    {
        _remoteControlService = remoteControlService;
        _remoteControlService.StateChanged += HandleRemoteControlStateChanged;
        _pageService = pageService;
        _monitorsService = monitorsService;
        _optionsService = optionsService;
        _thumbnailService = thumbnailService;
        _activeMediaItemsService = activeMediaItemsService;
        _snackbarService = snackbarService;
        _optionsService.RemoteControlPinChangedEvent += (_, _) =>
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => OnPropertyChanged(nameof(RemoteControlPin)));

        _recentlyUsedMediaFolders = new RecentlyUsedFolders();
        InitRecentlyUsedFolders();

        _monitors = GetSystemMonitors();
        _languages = GetSupportedLanguages();
        _loggingLevels = GetLoggingLevels();
        _fadingTypes = GetImageFadingTypes();
        _fadingSpeeds = GetFadingSpeedTypes();
        _renderingMethods = GetRenderingMethods();
        _magnifierShapes = GetMagnifierShapes();
        _magnifierSizes = GetMagnifierSizes();
        _mirrorHotKeys = GetMirrorHotKeys();
        _appModes = GetAppModes();

        _pageService.NavigationEvent += HandleNavigationEvent;
        _optionsService.SortModeChangedEvent += HandleSortModeChangedEvent;
        _optionsService.MediaFolderChangedEvent += (_, _) => ResetManualOrderCommand.NotifyCanExecuteChanged();

        InitCommands();
        WeakReferenceMessenger.Default.Register<ShutDownMessage>(this, OnShutDown);
    }

    public RelayCommand Set360PSizeCommand { get; private set; } = null!;

    public RelayCommand Set480PSizeCommand { get; private set; } = null!;

    public RelayCommand Set720PSizeCommand { get; private set; } = null!;

    public RelayCommand Set1080PSizeCommand { get; private set; } = null!;

    public RelayCommand PurgeThumbnailCacheCommand { get; private set; } = null!;

    public RelayCommand PurgeWebCacheCommand { get; private set; } = null!;

    public AsyncRelayCommand ResetManualOrderCommand { get; private set; } = null!;

    public AsyncRelayCommand InstallCloudflaredCommand { get; private set; } = null!;

    public AsyncRelayCommand CreateFirewallExceptionCommand { get; private set; } = null!;

    public System.Windows.Visibility CreateFirewallExceptionVisibility =>
        _optionsService.RemoteControlEnabled &&
        _optionsService.RemoteControlMode == RemoteControlMode.Local
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;

    /// <summary>
    /// Gets the warning shown when a connected network is Public and the firewall exception doesn't cover Public
    /// networks (so phones on it can't connect), or null.
    /// </summary>
    public string? RemoteControlPublicNetworkWarning { get; private set; }

    public System.Windows.Visibility RemoteControlPublicNetworkWarningVisibility =>
        RemoteControlPublicNetworkWarning == null ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;

    public System.Windows.Visibility InstallCloudflaredVisibility =>
        _optionsService.RemoteControlEnabled &&
        _optionsService.RemoteControlMode == RemoteControlMode.Cloudflare &&
        (_installingCloudflared || CloudflareTunnel.LocateCloudflared() == null)
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;

    public RelayCommand RetryTunnelCommand { get; private set; } = null!;

    public RelayCommand OpenRemoteControlUrlCommand { get; private set; } = null!;

    public RelayCommand CopyRemoteControlUrlCommand { get; private set; } = null!;

    /// <summary>
    /// Gets a value indicating whether cloudflared is being installed or the tunnel is starting.
    /// </summary>
    public bool IsRemoteControlBusy =>
        _installingCloudflared ||
        (_optionsService.RemoteControlEnabled &&
         _optionsService.RemoteControlMode == RemoteControlMode.Cloudflare &&
         _remoteControlService.TunnelState is TunnelState.Starting or TunnelState.WaitingForUrl);

    public System.Windows.Visibility RemoteControlProgressVisibility =>
        IsRemoteControlBusy ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public bool RemoteControlModeEnabled => _optionsService.RemoteControlEnabled && !IsRemoteControlBusy;

    public System.Windows.Visibility RetryTunnelVisibility =>
        _optionsService.RemoteControlEnabled &&
        _optionsService.RemoteControlMode == RemoteControlMode.Cloudflare &&
        !_installingCloudflared &&
        _remoteControlService.TunnelState == TunnelState.Failed &&
        CloudflareTunnel.LocateCloudflared() != null
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;

    /// <summary>
    /// Gets "Using cloudflared: path" in Cloudflare mode when cloudflared is found, otherwise null.
    /// </summary>
    public string? CloudflaredPathText
    {
        get
        {
            if (!_optionsService.RemoteControlEnabled || _optionsService.RemoteControlMode != RemoteControlMode.Cloudflare)
            {
                return null;
            }

            var path = CloudflareTunnel.LocateCloudflared();
#pragma warning disable CA1863
            return path == null ? null : string.Format(CultureInfo.CurrentCulture, Properties.Resources.REMOTE_CONTROL_USING_CLOUDFLARED, path);
#pragma warning restore CA1863
        }
    }

    public System.Windows.Visibility CloudflaredPathVisibility =>
        CloudflaredPathText == null ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;

    public string InstallCloudflaredButtonText => _cloudflaredInstallFailed
        ? Properties.Resources.REMOTE_CONTROL_RETRY
        : Properties.Resources.REMOTE_CONTROL_INSTALL_CLOUDFLARED;

    public RelayCommand OpenMediaFolderCommand { get; private set; } = null!;

    public ObservableCollection<string> RecentMediaFolders => _recentlyUsedMediaFolders.GetFolders();

#pragma warning disable CA1863
    public static string AppVersionStr => string.Format(CultureInfo.CurrentCulture, Properties.Resources.APP_VER, VersionDetection.GetCurrentVersion());
#pragma warning restore CA1863

    public IEnumerable<ImageFadeSpeed> FadeSpeedTypes => _fadingSpeeds;

    public IEnumerable<ImageFade> ImageFadeTypes => _fadingTypes;

    public IEnumerable<MagnifierShapeItem> MagnifierShapes => _magnifierShapes;

    public IEnumerable<MagnifierSizeItem> MagnifierSizes => _magnifierSizes;

    public IEnumerable<MirrorHotKeyItem> MirrorHotKeys => _mirrorHotKeys;

    public bool IsBrowserCachePurgeQueued => _optionsService.ShouldPurgeBrowserCacheOnStartup;

    // ReSharper disable once MemberCanBeMadeStatic.Global
#pragma warning disable CA1822
    public double MinMirrorZoom => Options.MinMirrorZoom;

    // ReSharper disable once MemberCanBeMadeStatic.Global
    public double MaxMirrorZoom => Options.MaxMirrorZoom;
#pragma warning restore CA1822

    public string MaxItemCount
    {
        get => _optionsService.MaxItemCount.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (!string.IsNullOrEmpty(value) &&
                !_optionsService.MaxItemCount.ToString(CultureInfo.InvariantCulture).Equals(value, StringComparison.Ordinal) &&
                int.TryParse(value, out var count))
            {
                _optionsService.MaxItemCount = count;
                OnPropertyChanged();
            }
        }
    }

    public string RemoteControlPort
    {
        get => _optionsService.RemoteControlPort.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var port) &&
                port is >= 1 and <= 65535 &&
                port != _optionsService.RemoteControlPort)
            {
                _previousRemoteControlPort ??= _optionsService.RemoteControlPort;
                _optionsService.RemoteControlPort = port;
                if (_optionsService.RemoteControlEnabled)
                {
                    // Apply the new port now: restarts the listener and, in Cloudflare mode, the tunnel. Stopping
                    // cloudflared and rebinding can take a while, so do it off the UI thread; the service serialises
                    // restarts with its lifecycle lock (rapid edits end on the latest port) and raises StateChanged.
                    Task.Run(RestartRemoteControl);
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(RemoteControlAddresses));
                RaiseRemoteControlStateChanged();
            }
        }
    }

    public bool RemoteControlEnabled
    {
        get => _optionsService.RemoteControlEnabled;
        set
        {
            if (_optionsService.RemoteControlEnabled != value)
            {
                _optionsService.RemoteControlEnabled = value; // service applies the change live
                OnPropertyChanged();
                RaiseRemoteControlStateChanged();
            }
        }
    }

    public IEnumerable<RemoteControlModeItem> RemoteControlModes => _remoteControlModes;

    /// <summary>
    /// Gets or sets the remote control PIN: 4-8 digits, or empty for none in Local mode.
    /// Cloudflare mode needs at least 6 digits (a 6-digit PIN is generated automatically if none is set).
    /// </summary>
    public string RemoteControlPin
    {
        get => _optionsService.RemoteControlPin ?? string.Empty;
        set
        {
            value = value?.Trim() ?? string.Empty;
            if (value.Length > 0 && !Options.IsValidRemoteControlPin(value))
            {
                throw new ArgumentException(Properties.Resources.REMOTE_CONTROL_PIN_INVALID);
            }

            if (_optionsService.RemoteControlEnabled &&
                _optionsService.RemoteControlMode == RemoteControlMode.Cloudflare &&
                !Options.IsValidCloudflarePin(value))
            {
                throw new ArgumentException(Properties.Resources.REMOTE_CONTROL_PIN_TOO_SHORT_CLOUDFLARE);
            }

            if (value != RemoteControlPin)
            {
                _optionsService.RemoteControlPin = value;
                OnPropertyChanged();
            }
        }
    }

    public RemoteControlMode RemoteControlMode
    {
        get => _optionsService.RemoteControlMode;
        set
        {
            if (_optionsService.RemoteControlMode != value)
            {
                _optionsService.RemoteControlMode = value; // service applies the change live
                OnPropertyChanged();
                RaiseRemoteControlStateChanged();
            }
        }
    }

    /// <summary>
    /// Gets the URL a phone should open: the Cloudflare URL in Cloudflare mode (once known), otherwise the first LAN address.
    /// </summary>
    public string? RemoteControlActiveUrl
    {
        get
        {
            if (!_optionsService.RemoteControlEnabled)
            {
                return null;
            }

            if (_optionsService.RemoteControlMode == RemoteControlMode.Cloudflare)
            {
                return _remoteControlService.TunnelUrl;
            }

            if (_remoteControlService.IsLocalhostOnly)
            {
                // a LAN address would not work, so do not advertise one (or a QR code)
                return null;
            }

            var first = RemoteControlAddresses.Split(Environment.NewLine).FirstOrDefault();
            return first is null or "-" ? null : first;
        }
    }

    public string RemoteControlStatus
    {
        get
        {
            if (!_optionsService.RemoteControlEnabled)
            {
                return Properties.Resources.REMOTE_CONTROL_STATUS_OFF;
            }

            if (_remoteControlService.PortInUse is { } portInUse)
            {
#pragma warning disable CA1863
                return string.Format(CultureInfo.CurrentCulture, Properties.Resources.REMOTE_CONTROL_PORT_IN_USE, portInUse);
#pragma warning restore CA1863
            }

            if (_optionsService.RemoteControlMode == RemoteControlMode.Cloudflare)
            {
                if (_installingCloudflared)
                {
                    return Properties.Resources.REMOTE_CONTROL_INSTALLING_CLOUDFLARED;
                }

                if (_cloudflaredInstallFailed && _remoteControlService.TunnelError == Properties.Resources.REMOTE_CONTROL_TUNNEL_CLOUDFLARED_NOT_FOUND)
                {
                    return Properties.Resources.REMOTE_CONTROL_INSTALL_CLOUDFLARED_FAILED;
                }

                switch (_remoteControlService.TunnelState)
                {
                    case TunnelState.Starting:
                        return Properties.Resources.REMOTE_CONTROL_STATUS_TUNNEL_STARTING;
                    case TunnelState.WaitingForUrl:
                        return Properties.Resources.REMOTE_CONTROL_TUNNEL_WAITING_URL;
                    case TunnelState.Connected:
#pragma warning disable CA1863
                        return string.Format(CultureInfo.CurrentCulture, Properties.Resources.REMOTE_CONTROL_TUNNEL_CONNECTED, _remoteControlService.TunnelUrl);
#pragma warning restore CA1863
                    case TunnelState.Failed:
                        return _remoteControlService.TunnelError ?? Properties.Resources.REMOTE_CONTROL_STATUS_NOT_RUNNING;
                    default:
                        if (_remoteControlService.TunnelError != null)
                        {
                            return _remoteControlService.TunnelError;
                        }

                        if (_remoteControlService.TunnelUrl == null)
                        {
                            return Properties.Resources.REMOTE_CONTROL_STATUS_TUNNEL_STARTING;
                        }

                        break;
                }
            }

            if (_remoteControlService.IsLocalhostOnly)
            {
#pragma warning disable CA1863
                return string.Format(CultureInfo.CurrentCulture, Properties.Resources.REMOTE_CONTROL_LOCALHOST_ONLY, _optionsService.RemoteControlPort);
#pragma warning restore CA1863
            }

            return _remoteControlService.IsRunning
                ? Properties.Resources.REMOTE_CONTROL_ADDRESS
                : Properties.Resources.REMOTE_CONTROL_STATUS_NOT_RUNNING;
        }
    }

    public System.Windows.Media.Imaging.BitmapImage? RemoteControlQrCode
    {
        get
        {
            var url = RemoteControlActiveUrl;
            if (url != _qrCodeUrl)
            {
                _qrCodeUrl = url;
                _remoteControlQrCode = url == null ? null : CreateQrCode(url);
            }

            return _remoteControlQrCode;
        }
    }

    /// <summary>
    /// Gets the visibility of the QR code image: collapsed when there is no address to encode,
    /// so the empty 180x180 placeholder does not take up space.
    /// </summary>
    public System.Windows.Visibility RemoteControlQrVisibility =>
        RemoteControlQrCode == null ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;

    public System.Windows.Visibility RemoteControlUrlVisibility =>
        RemoteControlActiveUrl == null ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;

    public string RemoteControlAddresses
    {
        get
        {
            try
            {
                var port = _optionsService.RemoteControlPort;
                var addresses = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                                n.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                    .Select(n => n.GetIPProperties())
                    .Where(p => p.GatewayAddresses.Count > 0)
                    .SelectMany(p => p.UnicastAddresses)
                    .Select(a => a.Address)
                    .Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                                !a.ToString().StartsWith("169.254.", StringComparison.Ordinal))
                    .Select(a => $"http://{a}:{port}/")
                    .Distinct()
                    .ToList();

                return addresses.Count > 0 ? string.Join(Environment.NewLine, addresses) : "-";
            }
            catch (System.Net.NetworkInformation.NetworkInformationException)
            {
                return "-";
            }
        }
    }

    public DateTime MediaCalendarDate
    {
        get => _optionsService.OperatingDate;
        set
        {
            if (_optionsService.OperatingDate != value)
            {
                _optionsService.OperatingDate = value;
                OnPropertyChanged();
            }
        }
    }

    public bool AlwaysOnTop
    {
        get => _optionsService.AlwaysOnTop;
        set
        {
            if (_optionsService.AlwaysOnTop != value)
            {
                _optionsService.AlwaysOnTop = value;
                OnPropertyChanged();
            }
        }
    }

    public int VideoScreenLeftMargin
    {
        get => _optionsService.VideoScreenPosition.LeftMarginPercentage;
        set
        {
            if (_optionsService.VideoScreenPosition.LeftMarginPercentage != value)
            {
                var newPos = (ScreenPosition)_optionsService.VideoScreenPosition.Clone();
                ScreenPositionHelper.ModifyScreenPosition(newPos, ScreenMarginSide.Left, value, out var opposingMarginChanged);

                _optionsService.VideoScreenPosition = newPos;
                OnPropertyChanged();

                if (opposingMarginChanged)
                {
                    OnPropertyChanged(nameof(VideoScreenRightMargin));
                }
            }
        }
    }

    public int VideoScreenRightMargin
    {
        get => _optionsService.VideoScreenPosition.RightMarginPercentage;
        set
        {
            if (_optionsService.VideoScreenPosition.RightMarginPercentage != value)
            {
                var newPos = (ScreenPosition)_optionsService.VideoScreenPosition.Clone();
                ScreenPositionHelper.ModifyScreenPosition(newPos, ScreenMarginSide.Right, value, out var opposingMarginChanged);

                _optionsService.VideoScreenPosition = newPos;
                OnPropertyChanged();

                if (opposingMarginChanged)
                {
                    OnPropertyChanged(nameof(VideoScreenLeftMargin));
                }
            }
        }
    }

    public int VideoScreenTopMargin
    {
        get => _optionsService.VideoScreenPosition.TopMarginPercentage;
        set
        {
            if (_optionsService.VideoScreenPosition.TopMarginPercentage != value)
            {
                var newPos = (ScreenPosition)_optionsService.VideoScreenPosition.Clone();
                ScreenPositionHelper.ModifyScreenPosition(newPos, ScreenMarginSide.Top, value, out var opposingMarginChanged);

                _optionsService.VideoScreenPosition = newPos;
                OnPropertyChanged();

                if (opposingMarginChanged)
                {
                    OnPropertyChanged(nameof(VideoScreenBottomMargin));
                }
            }
        }
    }

    public int VideoScreenBottomMargin
    {
        get => _optionsService.VideoScreenPosition.BottomMarginPercentage;
        set
        {
            if (_optionsService.VideoScreenPosition.BottomMarginPercentage != value)
            {
                var newPos = (ScreenPosition)_optionsService.VideoScreenPosition.Clone();
                ScreenPositionHelper.ModifyScreenPosition(newPos, ScreenMarginSide.Bottom, value, out var opposingMarginChanged);

                _optionsService.VideoScreenPosition = newPos;
                OnPropertyChanged();

                if (opposingMarginChanged)
                {
                    OnPropertyChanged(nameof(VideoScreenTopMargin));
                }
            }
        }
    }

    public int ImageScreenLeftMargin
    {
        get => _optionsService.ImageScreenPosition.LeftMarginPercentage;
        set
        {
            if (_optionsService.ImageScreenPosition.LeftMarginPercentage != value)
            {
                var newPos = (ScreenPosition)_optionsService.ImageScreenPosition.Clone();
                ScreenPositionHelper.ModifyScreenPosition(newPos, ScreenMarginSide.Left, value, out var opposingMarginChanged);

                _optionsService.ImageScreenPosition = newPos;
                OnPropertyChanged();

                if (opposingMarginChanged)
                {
                    OnPropertyChanged(nameof(ImageScreenRightMargin));
                }
            }
        }
    }

    public int ImageScreenRightMargin
    {
        get => _optionsService.ImageScreenPosition.RightMarginPercentage;
        set
        {
            if (_optionsService.ImageScreenPosition.RightMarginPercentage != value)
            {
                var newPos = (ScreenPosition)_optionsService.ImageScreenPosition.Clone();
                ScreenPositionHelper.ModifyScreenPosition(newPos, ScreenMarginSide.Right, value, out var opposingMarginChanged);

                _optionsService.ImageScreenPosition = newPos;
                OnPropertyChanged();

                if (opposingMarginChanged)
                {
                    OnPropertyChanged(nameof(ImageScreenLeftMargin));
                }
            }
        }
    }

    public int ImageScreenTopMargin
    {
        get => _optionsService.ImageScreenPosition.TopMarginPercentage;
        set
        {
            if (_optionsService.ImageScreenPosition.TopMarginPercentage != value)
            {
                var newPos = (ScreenPosition)_optionsService.ImageScreenPosition.Clone();
                ScreenPositionHelper.ModifyScreenPosition(newPos, ScreenMarginSide.Top, value, out var opposingMarginChanged);

                _optionsService.ImageScreenPosition = newPos;
                OnPropertyChanged();

                if (opposingMarginChanged)
                {
                    OnPropertyChanged(nameof(ImageScreenBottomMargin));
                }
            }
        }
    }

    public int ImageScreenBottomMargin
    {
        get => _optionsService.ImageScreenPosition.BottomMarginPercentage;
        set
        {
            if (_optionsService.ImageScreenPosition.BottomMarginPercentage != value)
            {
                var newPos = (ScreenPosition)_optionsService.ImageScreenPosition.Clone();
                ScreenPositionHelper.ModifyScreenPosition(newPos, ScreenMarginSide.Bottom, value, out var opposingMarginChanged);

                _optionsService.ImageScreenPosition = newPos;
                OnPropertyChanged();

                if (opposingMarginChanged)
                {
                    OnPropertyChanged(nameof(ImageScreenTopMargin));
                }
            }
        }
    }

    public int WebScreenLeftMargin
    {
        get => _optionsService.WebScreenPosition.LeftMarginPercentage;
        set
        {
            if (_optionsService.WebScreenPosition.LeftMarginPercentage != value)
            {
                var newPos = (ScreenPosition)_optionsService.WebScreenPosition.Clone();
                ScreenPositionHelper.ModifyScreenPosition(newPos, ScreenMarginSide.Left, value, out var opposingMarginChanged);

                _optionsService.WebScreenPosition = newPos;
                OnPropertyChanged();

                if (opposingMarginChanged)
                {
                    OnPropertyChanged(nameof(WebScreenRightMargin));
                }
            }
        }
    }

    public int WebScreenRightMargin
    {
        get => _optionsService.WebScreenPosition.RightMarginPercentage;
        set
        {
            if (_optionsService.WebScreenPosition.RightMarginPercentage != value)
            {
                var newPos = (ScreenPosition)_optionsService.WebScreenPosition.Clone();
                ScreenPositionHelper.ModifyScreenPosition(newPos, ScreenMarginSide.Right, value, out var opposingMarginChanged);

                _optionsService.WebScreenPosition = newPos;
                OnPropertyChanged();

                if (opposingMarginChanged)
                {
                    OnPropertyChanged(nameof(WebScreenLeftMargin));
                }
            }
        }
    }

    public int WebScreenTopMargin
    {
        get => _optionsService.WebScreenPosition.TopMarginPercentage;
        set
        {
            if (_optionsService.WebScreenPosition.TopMarginPercentage != value)
            {
                var newPos = (ScreenPosition)_optionsService.WebScreenPosition.Clone();
                ScreenPositionHelper.ModifyScreenPosition(newPos, ScreenMarginSide.Top, value, out var opposingMarginChanged);

                _optionsService.WebScreenPosition = newPos;
                OnPropertyChanged();

                if (opposingMarginChanged)
                {
                    OnPropertyChanged(nameof(WebScreenBottomMargin));
                }
            }
        }
    }

    public int WebScreenBottomMargin
    {
        get => _optionsService.WebScreenPosition.BottomMarginPercentage;
        set
        {
            if (_optionsService.WebScreenPosition.BottomMarginPercentage != value)
            {
                var newPos = (ScreenPosition)_optionsService.WebScreenPosition.Clone();
                ScreenPositionHelper.ModifyScreenPosition(newPos, ScreenMarginSide.Bottom, value, out var opposingMarginChanged);

                _optionsService.WebScreenPosition = newPos;
                OnPropertyChanged();

                if (opposingMarginChanged)
                {
                    OnPropertyChanged(nameof(WebScreenTopMargin));
                }
            }
        }
    }

    public bool AllowMirror
    {
        get => _optionsService.AllowMirror;
        set
        {
            if (_optionsService.AllowMirror != value)
            {
                _optionsService.AllowMirror = value;
                OnPropertyChanged();
            }
        }
    }

    public bool MirrorByDefault
    {
        get => _optionsService.UseMirrorByDefault;
        set
        {
            if (_optionsService.UseMirrorByDefault != value)
            {
                _optionsService.UseMirrorByDefault = value;
                OnPropertyChanged();
            }
        }
    }

    public double MirrorZoom
    {
        get => _optionsService.MirrorZoom;
        set
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (_optionsService.MirrorZoom != value)
            {
                _optionsService.MirrorZoom = value;
                OnPropertyChanged();
            }
        }
    }

    public char MirrorHotKey
    {
        get => _optionsService.MirrorHotKey;
        set
        {
            if (_optionsService.MirrorHotKey != value)
            {
                _optionsService.MirrorHotKey = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowCommandPanel
    {
        get => _optionsService.ShowMediaItemCommandPanel;
        set
        {
            if (_optionsService.ShowMediaItemCommandPanel != value)
            {
                _optionsService.ShowMediaItemCommandPanel = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowSortModeToggleButton
    {
        get => _optionsService.ShowSortModeToggleButton;
        set
        {
            if (_optionsService.ShowSortModeToggleButton != value)
            {
                _optionsService.ShowSortModeToggleButton = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsAutoSortMode
    {
        get => _optionsService.SortMode == MediaSortMode.Auto;
        set
        {
            if (value && _optionsService.SortMode != MediaSortMode.Auto)
            {
                _optionsService.SortMode = MediaSortMode.Auto;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsManualSortMode));
            }
        }
    }

    public bool IsManualSortMode
    {
        get => _optionsService.SortMode == MediaSortMode.Manual;
        set
        {
            if (value && _optionsService.SortMode != MediaSortMode.Manual)
            {
                _optionsService.SortMode = MediaSortMode.Manual;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsAutoSortMode));
            }
        }
    }

    public bool UseInternalMediaTitles
    {
        get => _optionsService.UseInternalMediaTitles;
        set
        {
            if (_optionsService.UseInternalMediaTitles != value)
            {
                _optionsService.UseInternalMediaTitles = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IncludeBlankScreenItem
    {
        get => _optionsService.IncludeBlankScreenItem;
        set
        {
            if (_optionsService.IncludeBlankScreenItem != value)
            {
                _optionsService.IncludeBlankScreenItem = value;
                OnPropertyChanged();
            }
        }
    }

    public bool PermanentBackdrop
    {
        get => _optionsService.PermanentBackdrop;
        set
        {
            if (_optionsService.PermanentBackdrop != value)
            {
                _optionsService.PermanentBackdrop = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(NotPermanentBackdrop));
            }
        }
    }

    public bool NotPermanentBackdrop => !PermanentBackdrop;

    public bool ShowVideoSubtitles
    {
        get => _optionsService.ShowVideoSubtitles;
        set
        {
            if (_optionsService.ShowVideoSubtitles != value)
            {
                _optionsService.ShowVideoSubtitles = value;
                OnPropertyChanged();
            }
        }
    }

    public bool AllowVideoScrubbing
    {
        get => _optionsService.AllowVideoScrubbing;
        set
        {
            if (_optionsService.AllowVideoScrubbing != value)
            {
                _optionsService.AllowVideoScrubbing = value;
                OnPropertyChanged();
            }
        }
    }

    public bool AllowVideoPositionSeeking
    {
        get => _optionsService.AllowVideoPositionSeeking;
        set
        {
            if (_optionsService.AllowVideoPositionSeeking != value)
            {
                _optionsService.AllowVideoPositionSeeking = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ConfirmWhenStoppingVideo
    {
        get => _optionsService.ConfirmVideoStop;
        set
        {
            if (_optionsService.ConfirmVideoStop != value)
            {
                _optionsService.ConfirmVideoStop = value;
                OnPropertyChanged();
            }
        }
    }

    public bool AllowVideoPause
    {
        get => _optionsService.AllowVideoPause;
        set
        {
            if (_optionsService.AllowVideoPause != value)
            {
                _optionsService.AllowVideoPause = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the volume button is shown in the toolbar (default off).
    /// Turning it off resets the volume to 100%.
    /// </summary>
    public bool ShowVolumeControl
    {
        get => _optionsService.ShowVolumeControl;
        set
        {
            if (_optionsService.ShowVolumeControl != value)
            {
                _optionsService.ShowVolumeControl = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the remote control page offers volume control (default off).
    /// </summary>
    public bool ShowRemoteVolumeControl
    {
        get => _optionsService.ShowRemoteVolumeControl;
        set
        {
            if (_optionsService.ShowRemoteVolumeControl != value)
            {
                _optionsService.ShowRemoteVolumeControl = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether 'Create firewall exception' also opens the port on Public networks
    /// (default off: Private networks only).
    /// </summary>
    public bool RemoteControlAllowPublicNetworks
    {
        get => _optionsService.RemoteControlAllowPublicNetworks;
        set
        {
            if (_optionsService.RemoteControlAllowPublicNetworks != value)
            {
                _optionsService.RemoteControlAllowPublicNetworks = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowFreezeCommand
    {
        get => _optionsService.ShowFreezeCommand;
        set
        {
            if (_optionsService.ShowFreezeCommand != value)
            {
                _optionsService.ShowFreezeCommand = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowMediaItemCountBadge
    {
        get => _optionsService.ShowMediaItemCountBadge;
        set
        {
            if (_optionsService.ShowMediaItemCountBadge != value)
            {
                _optionsService.ShowMediaItemCountBadge = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsMediaFolderOverriden => _optionsService.IsCommandLineMediaFolderSpecified();

    public bool IsMediaInactive => !IsMediaActive;

    public bool EmbeddedThumbnails
    {
        get => _optionsService.EmbeddedThumbnails;
        set
        {
            if (_optionsService.EmbeddedThumbnails != value)
            {
                _optionsService.EmbeddedThumbnails = value;
                OnPropertyChanged();
                PurgeThumbnailCache();
            }
        }
    }

    public bool CacheImages
    {
        get => _optionsService.CacheImages;
        set
        {
            if (_optionsService.CacheImages != value)
            {
                _optionsService.CacheImages = value;
                OnPropertyChanged();
            }
        }
    }

    public bool AutoRotateImages
    {
        get => _optionsService.AutoRotateImages;
        set
        {
            if (_optionsService.AutoRotateImages != value)
            {
                _optionsService.AutoRotateImages = value;
                OnPropertyChanged();
            }
        }
    }

    public string MediaFolder
    {
        get => _optionsService.MediaFolder;
        set
        {
            if (_optionsService.MediaFolder != value)
            {
                _optionsService.MediaFolder = value;
                OnPropertyChanged();
            }
        }
    }

    public ImageFadeType ImageFadeType
    {
        get => _optionsService.ImageFadeType;
        set
        {
            if (_optionsService.ImageFadeType != value)
            {
                _optionsService.ImageFadeType = value;
                OnPropertyChanged();
            }
        }
    }

    public MagnifierShape MagnifierShape
    {
        get => _optionsService.MagnifierShape;
        set
        {
            if (_optionsService.MagnifierShape != value)
            {
                _optionsService.MagnifierShape = value;
                OnPropertyChanged();
            }
        }
    }

    public MagnifierSize MagnifierSize
    {
        get => _optionsService.MagnifierSize;
        set
        {
            if (_optionsService.MagnifierSize != value)
            {
                _optionsService.MagnifierSize = value;
                OnPropertyChanged();
            }
        }
    }

    public double MagnifierZoomLevel
    {
        get => _optionsService.MagnifierZoomLevel;
        set
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (_optionsService.MagnifierZoomLevel != value)
            {
                _optionsService.MagnifierZoomLevel = value;
                OnPropertyChanged();
            }
        }
    }

    public double MagnifierFrameThickness
    {
        get => _optionsService.MagnifierFrameThickness;
        set
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (_optionsService.MagnifierFrameThickness != value)
            {
                _optionsService.MagnifierFrameThickness = value;
                OnPropertyChanged();
            }
        }
    }

    public double WebPageZoomIncrement
    {
        get => _optionsService.BrowserZoomLevelIncrement;
        set
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (_optionsService.BrowserZoomLevelIncrement != value)
            {
                _optionsService.BrowserZoomLevelIncrement = value;
                OnPropertyChanged();
            }
        }
    }

    public FadeSpeed FadeSpeedType
    {
        get => _optionsService.ImageFadeSpeed;
        set
        {
            if (_optionsService.ImageFadeSpeed != value)
            {
                _optionsService.ImageFadeSpeed = value;
                OnPropertyChanged();
            }
        }
    }

    public IEnumerable<LoggingLevel> LoggingLevels => _loggingLevels;

    public LogEventLevel LogEventLevel
    {
        get => _optionsService.LogEventLevel;
        set
        {
            if (_optionsService.LogEventLevel != value)
            {
                _optionsService.LogEventLevel = value;
                OnPropertyChanged();
            }
        }
    }

    public IEnumerable<RenderingMethodItem> RenderingMethods => _renderingMethods;

    public RenderingMethod RenderingMethod
    {
        get => _optionsService.RenderingMethod;
        set
        {
            if (_optionsService.RenderingMethod != value)
            {
                _optionsService.RenderingMethod = value;
                OnPropertyChanged();
            }
        }
    }

    public IEnumerable<AppModeItem> AppModes => _appModes;

    public DarkModeOption AppMode
    {
        get => _optionsService.DarkModeOption;
        set
        {
            if (_optionsService.DarkModeOption != value)
            {
                _optionsService.DarkModeOption = value;
                OnPropertyChanged();
            }
        }
    }

    public IEnumerable<LanguageItem> Languages => _languages;

    public string? LanguageId
    {
        get => _optionsService.Culture;
        set
        {
            if (_optionsService.Culture != value)
            {
                _optionsService.Culture = value;
                OnPropertyChanged();
            }
        }
    }

    public IEnumerable<MonitorItem> Monitors => _monitors;

    public bool CanChangeMonitor => !MediaWindowed;

    public string? MonitorId
    {
        get => _optionsService.MediaMonitorId;
        set
        {
            if (_optionsService.MediaMonitorId != value)
            {
                if (value == null && IsMediaActive)
                {
                    // prevent selection of "none" when media is active.
                    _snackbarService.EnqueueWithOk(Properties.Resources.NO_DESELECT_MONITOR, Properties.Resources.OK);
                }
                else
                {
                    _optionsService.MediaMonitorId = value;
                    OnPropertyChanged();
                }
            }
        }
    }

    public bool WindowedAlwaysOnTop
    {
        get => _optionsService.WindowedAlwaysOnTop;
        set
        {
            if (_optionsService.WindowedAlwaysOnTop != value)
            {
                _optionsService.WindowedAlwaysOnTop = value;
                OnPropertyChanged();
            }
        }
    }

    public bool MediaWindowed
    {
        get => _optionsService.MediaWindowed;
        set
        {
            if (_optionsService.MediaWindowed != value)
            {
                if (!value && IsMediaActive && _optionsService.MediaMonitorId == null)
                {
                    // prevent unchecking of windowed mode when media is active.
                    _snackbarService.EnqueueWithOk(Properties.Resources.NO_DESELECT_WINDOWED, Properties.Resources.OK);
                }
                else
                {
                    _optionsService.MediaWindowed = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanChangeMonitor));
                }
            }
        }
    }

    public bool Is360PSize => MediaWindowSize == Size360P;

    public bool Is480PSize => MediaWindowSize == Size480P;

    public bool Is720PSize => MediaWindowSize == Size720P;

    public bool Is1080PSize => MediaWindowSize == Size1080P;

    public bool MediaWindowResizable
    {
        get => MediaWindowSize.IsEmpty;
        set
        {
            if (value)
            {
                MediaWindowSize = Size.Empty;
                OnPropertyChanged();
            }
        }
    }

    public bool MediaWindowFixed
    {
        get => !MediaWindowSize.IsEmpty;
        set
        {
            if (value)
            {
                MediaWindowSize = Size720P;
                OnPropertyChanged();
            }
        }
    }

    public int? MediaWindowWidth
    {
        get => MediaWindowSize.IsEmpty ? null : (int)MediaWindowSize.Width;
        set
        {
            if ((value ?? 0) < 0)
            {
                return;
            }

            MediaWindowSize = value.HasValue ? new Size(value.Value, MediaWindowSize.Height) : Size.Empty;
        }
    }

    public int? MediaWindowHeight
    {
        get => MediaWindowSize.IsEmpty ? null : (int)MediaWindowSize.Height;
        set
        {
            if ((value ?? 0) < 0)
            {
                return;
            }

            MediaWindowSize = value.HasValue ? new Size(MediaWindowSize.Width, value.Value) : Size.Empty;
        }
    }

    private Size MediaWindowSize
    {
        get => _optionsService.MediaWindowSize;
        set
        {
            _optionsService.MediaWindowSize = NormalizeSize(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(MediaWindowResizable));
            OnPropertyChanged(nameof(MediaWindowFixed));

            OnPropertyChanged(nameof(MediaWindowWidth));
            OnPropertyChanged(nameof(MediaWindowHeight));

            OnPropertyChanged(nameof(Is360PSize));
            OnPropertyChanged(nameof(Is480PSize));
            OnPropertyChanged(nameof(Is720PSize));
            OnPropertyChanged(nameof(Is1080PSize));
        }
    }

    private bool IsMediaActive
    {
        get;
        set
        {
            if (field != value)
            {
                SetProperty(ref field, value);
                OnPropertyChanged(nameof(IsMediaInactive));
            }
        }
    }

    private static Size NormalizeSize(Size value)
    {
        if (value.IsEmpty)
        {
            return value;
        }

        return new Size
        {
            Width = LimitNumber(MinSize.Width, value.Width, MaxSize.Width),
            Height = LimitNumber(MinSize.Height, value.Height, MaxSize.Height)
        };
    }

    private static double LimitNumber(double lowerLimitIncl, double value, double upperLimitIncl)
    {
        if (value < lowerLimitIncl)
        {
            return lowerLimitIncl;
        }

        if (value > upperLimitIncl)
        {
            return upperLimitIncl;
        }

        return value;
    }

    private void OnShutDown(object? sender, ShutDownMessage obj)
    {
        _optionsService.RecentlyUsedMediaFolders = _recentlyUsedMediaFolders.GetFolders().ToList();
        _optionsService.Save();
    }

    private static MagnifierShapeItem[] GetMagnifierShapes()
    {
        var values = Enum.GetValues<MagnifierShape>();
        var result = new List<MagnifierShapeItem>(values.Length);

        foreach (var v in values)
        {
            result.Add(new MagnifierShapeItem
            {
                Shape = v,
                Name = v.GetDescriptiveName(),
            });
        }

        return result.ToArray();
    }

    private static MagnifierSizeItem[] GetMagnifierSizes()
    {
        var result = new List<MagnifierSizeItem>();

        foreach (var v in Enum.GetValues<MagnifierSize>())
        {
            result.Add(new MagnifierSizeItem
            {
                Size = v,
                Name = v.GetDescriptiveName(),
            });
        }

        return result.ToArray();
    }

    private static ImageFadeSpeed[] GetFadingSpeedTypes()
    {
        var result = new List<ImageFadeSpeed>();

        foreach (var v in Enum.GetValues<FadeSpeed>())
        {
            result.Add(new ImageFadeSpeed
            {
                Speed = v,
                Name = v.GetDescriptiveName(),
            });
        }

        return result.ToArray();
    }

    private static ImageFade[] GetImageFadingTypes()
    {
        var result = new List<ImageFade>();

        foreach (var v in Enum.GetValues<ImageFadeType>())
        {
            result.Add(new ImageFade
            {
                Fade = v,
                Name = v.GetDescriptiveName(),
            });
        }

        return result.ToArray();
    }

    private static LoggingLevel[] GetLoggingLevels()
    {
        var result = new List<LoggingLevel>();

        foreach (var v in Enum.GetValues<LogEventLevel>())
        {
            result.Add(new LoggingLevel
            {
                Level = v,
                Name = v.GetDescriptiveName(),
            });
        }

        return result.ToArray();
    }

    private static RenderingMethodItem[] GetRenderingMethods() =>
        // don't localize these strings!
        [
        new() { Method = RenderingMethod.MediaFoundation, Name = "Media Foundation" },
        new() { Method = RenderingMethod.Ffmpeg, Name = "Ffmpeg" }
        ];

    private static AppModeItem[] GetAppModes() =>
        [
        new() { Mode = DarkModeOption.System, Name = Properties.Resources.APP_MODE_SYSTEM },
        new() { Mode = DarkModeOption.Dark, Name = Properties.Resources.APP_MODE_DARK },
        new() { Mode = DarkModeOption.Light, Name = Properties.Resources.APP_MODE_LIGHT },
        ];

    private MonitorItem[] GetSystemMonitors()
    {
        var result = new List<MonitorItem>
        {
            // empty (i.e. no timer monitor)
            new()
            {
                MonitorName = Properties.Resources.MONITOR_NONE,
                FriendlyName = Properties.Resources.MONITOR_NONE,
            },
        };

        var monitors = _monitorsService.GetSystemMonitors();
        result.AddRange(monitors.Select(x => new MonitorItem(x)));

        return result.ToArray();
    }

    private void InitRecentlyUsedFolders()
    {
        for (var n = _optionsService.RecentlyUsedMediaFolders.Count - 1; n >= 0; --n)
        {
            _recentlyUsedMediaFolders.Add(_optionsService.RecentlyUsedMediaFolders[n]);
        }
    }

    private void HandleNavigationEvent(object? sender, NavigationEventArgs e)
    {
        if (e.PageName != null && e.PageName.Equals(_pageService.SettingsPageName, StringComparison.Ordinal))
        {
            // when Settings page is shown.
            IsMediaActive = _activeMediaItemsService.Any();
            OnPropertyChanged(nameof(IsAutoSortMode));
            OnPropertyChanged(nameof(IsManualSortMode));
            RefreshPublicNetworkWarning(); // e.g. the network was switched to Private in Windows Settings
        }
    }

    private void RefreshPublicNetworkWarning()
    {
        RemoteControlPublicNetworkWarning = GetPublicNetworkWarning();
        OnPropertyChanged(nameof(RemoteControlPublicNetworkWarning));
        OnPropertyChanged(nameof(RemoteControlPublicNetworkWarningVisibility));
    }

    private string? GetPublicNetworkWarning()
    {
        if (!_optionsService.RemoteControlEnabled || _optionsService.RemoteControlMode != RemoteControlMode.Local)
        {
            return null;
        }

        var publicNetworks = NetworkFirewallInfo.GetConnectedPublicNetworks();
        if (publicNetworks.Count == 0 ||
            NetworkFirewallInfo.CoversPublic(NetworkFirewallInfo.GetFirewallRuleProfiles(FirewallRuleName(_optionsService.RemoteControlPort))))
        {
            return null;
        }

#pragma warning disable CA1863
        return string.Format(CultureInfo.CurrentCulture, Properties.Resources.REMOTE_CONTROL_PUBLIC_NETWORK_WARNING, string.Join(", ", publicNetworks));
#pragma warning restore CA1863
    }

    private void HandleSortModeChangedEvent(object? sender, EventArgs e)
    {
        ResetManualOrderCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsAutoSortMode));
        OnPropertyChanged(nameof(IsManualSortMode));
    }

    private bool CanResetManualOrder() => IsManualSortMode && !string.IsNullOrWhiteSpace(MediaFolder);

    private Task ResetManualOrderAsync() => CanResetManualOrder()
        ? WeakReferenceMessenger.Default.Send(new ResetManualOrderMessage()).Response
        : Task.CompletedTask;

    private static System.Windows.Media.Imaging.BitmapImage? CreateQrCode(string url)
    {
        try
        {
            var png = QRCoder.PngByteQRCodeHelper.GetQRCode(url, QRCoder.QRCodeGenerator.ECCLevel.M, 10);
            var image = new System.Windows.Media.Imaging.BitmapImage();
            using var stream = new MemoryStream(png);
            image.BeginInit();
            image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex)
        {
            Serilog.Log.Logger.Error(ex, "Could not create QR code for {Url}", url);
            return null;
        }
    }

    private void HandleRemoteControlStateChanged(object? sender, EventArgs e)
        => System.Windows.Application.Current?.Dispatcher.BeginInvoke(RaiseRemoteControlStateChanged);

    private void RaiseRemoteControlStateChanged()
    {
        OnPropertyChanged(nameof(RemoteControlStatus));
        OnPropertyChanged(nameof(InstallCloudflaredVisibility));
        OnPropertyChanged(nameof(CreateFirewallExceptionVisibility));
        OnPropertyChanged(nameof(RemoteControlActiveUrl));
        OnPropertyChanged(nameof(RemoteControlUrlVisibility));
        OnPropertyChanged(nameof(RemoteControlQrCode));
        OnPropertyChanged(nameof(RemoteControlQrVisibility));
        OnPropertyChanged(nameof(IsRemoteControlBusy));
        OnPropertyChanged(nameof(RemoteControlProgressVisibility));
        OnPropertyChanged(nameof(RemoteControlModeEnabled));
        OnPropertyChanged(nameof(RetryTunnelVisibility));
        OnPropertyChanged(nameof(InstallCloudflaredButtonText));
        OnPropertyChanged(nameof(CloudflaredPathText));
        OnPropertyChanged(nameof(CloudflaredPathVisibility));
        RefreshPublicNetworkWarning();
    }

    private async Task InstallCloudflaredAsync()
    {
        _installingCloudflared = true;
        _cloudflaredInstallFailed = false;
        InstallCloudflaredCommand.NotifyCanExecuteChanged();
        RaiseRemoteControlStateChanged();

        try
        {
            // Downloads the pinned release, verifies its SHA-256 and moves it into the per-user folder
            await CloudflaredInstaller.InstallAsync();
        }
        catch (Exception ex)
        {
            Serilog.Log.Logger.Error(ex, "cloudflared install failed");
        }
        finally
        {
            _installingCloudflared = false;
            InstallCloudflaredCommand.NotifyCanExecuteChanged();
        }

        var path = CloudflareTunnel.LocateCloudflared();
        if (path != null)
        {
            // starts the tunnel (with progress) now that cloudflared exists
            Serilog.Log.Logger.Information("cloudflared installed at {Path}; starting tunnel", path);
            _remoteControlService.RetryTunnel();
        }
        else
        {
            _cloudflaredInstallFailed = true;
            Serilog.Log.Logger.Error("cloudflared still not found after install");
        }

        RaiseRemoteControlStateChanged();
    }

    private async Task CreateFirewallExceptionAsync()
    {
        var port = _optionsService.RemoteControlPort;
        var oldPort = _previousRemoteControlPort is { } p && p != port ? p : (int?)null;

        try
        {
            Serilog.Log.Logger.Information(
                "Creating firewall exception (public networks: {AllowPublic}) and URL reservation for port {Port} (removing old port {OldPort})",
                _optionsService.RemoteControlAllowPublicNetworks,
                port,
                oldPort);

            // One elevated cmd (one UAC prompt): delete existing rule/reservation first so repeated clicks
            // do not create duplicates, remove those of a previous port, then add the rule and the reservation.
            // sddl=D:(A;;GX;;;WD) is "Everyone" in a language-neutral form.
            var steps = new List<string>();
            if (oldPort != null)
            {
                steps.AddRange(DeleteRemoteControlAccessCommands(oldPort.Value));
            }

            steps.AddRange(DeleteRemoteControlAccessCommands(port));

            // Private networks only unless the user opts in: Windows marks venue and hotel Wi-Fi as Public
            var profile = _optionsService.RemoteControlAllowPublicNetworks ? "private,public" : "private";
            var add = $"netsh advfirewall firewall add rule name=\"{FirewallRuleName(port)}\" dir=in action=allow protocol=TCP localport={port} profile={profile} && " +
                      $"netsh http add urlacl url=http://+:{port}/ sddl=\"D:(A;;GX;;;WD)\"";

            var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = true,
                Verb = "runas",
                Arguments = $"/s /c \"{string.Join(" & ", steps)} & {add}\"",
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
            };

            using var process = System.Diagnostics.Process.Start(psi)
                ?? throw new InvalidOperationException("Could not start elevated netsh");

            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                _previousRemoteControlPort = null;
                Serilog.Log.Logger.Information("Firewall exception and URL reservation created for port {Port}; restarting listener", port);
                _remoteControlService.Restart();
                RaiseRemoteControlStateChanged();
                var message = _remoteControlService.IsLocalhostOnly
                    ? Properties.Resources.REMOTE_CONTROL_FIREWALL_STILL_LOCALHOST
                    : Properties.Resources.REMOTE_CONTROL_FIREWALL_DONE;
                _snackbarService.EnqueueReplacingCurrent(string.Format(CultureInfo.CurrentCulture, message, port));
            }
            else
            {
                _snackbarService.EnqueueReplacingCurrent(Properties.Resources.REMOTE_CONTROL_FIREWALL_FAILED);
                Serilog.Log.Logger.Warning("netsh firewall rule exited with code {Code}", process.ExitCode);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Logger.Error(ex, "Failed to create firewall exception");
            _snackbarService.EnqueueReplacingCurrent(Properties.Resources.REMOTE_CONTROL_FIREWALL_ERROR);
        }
    }

    private void RestartRemoteControl()
    {
        try
        {
            _remoteControlService.Restart();
        }
        catch (Exception ex)
        {
            Serilog.Log.Logger.Error(ex, "Could not restart remote control on the new port");
        }
    }

    private static string FirewallRuleName(int port) => $"OnlyM Remote Control (port {port})";

    private static IEnumerable<string> DeleteRemoteControlAccessCommands(int port)
    {
        yield return $"netsh advfirewall firewall delete rule name=\"{FirewallRuleName(port)}\" >nul 2>&1";
        yield return $"netsh http delete urlacl url=http://+:{port}/ >nul 2>&1";
    }

    private void InitCommands()
    {
        InstallCloudflaredCommand = new AsyncRelayCommand(InstallCloudflaredAsync, () => !_installingCloudflared);
        CreateFirewallExceptionCommand = new AsyncRelayCommand(CreateFirewallExceptionAsync);
        RetryTunnelCommand = new RelayCommand(() => System.Threading.Tasks.Task.Run(_remoteControlService.RetryTunnel));
        OpenRemoteControlUrlCommand = new RelayCommand(OpenRemoteControlUrl);
        CopyRemoteControlUrlCommand = new RelayCommand(CopyRemoteControlUrl);
        ResetManualOrderCommand = new AsyncRelayCommand(ResetManualOrderAsync, CanResetManualOrder);
        PurgeThumbnailCacheCommand = new RelayCommand(PurgeThumbnailCache);
        PurgeWebCacheCommand = new RelayCommand(PurgeWebCache);
        OpenMediaFolderCommand = new RelayCommand(OpenMediaFolder);
        Set360PSizeCommand = new RelayCommand(() => SetMediaWindowSize(Size360P));
        Set480PSizeCommand = new RelayCommand(() => SetMediaWindowSize(Size480P));
        Set720PSizeCommand = new RelayCommand(() => SetMediaWindowSize(Size720P));
        Set1080PSizeCommand = new RelayCommand(() => SetMediaWindowSize(Size1080P));
    }

    private void PurgeWebCache()
    {
        _optionsService.ShouldPurgeBrowserCacheOnStartup = true;
        OnPropertyChanged(nameof(IsBrowserCachePurgeQueued));
    }

    private void OpenRemoteControlUrl()
    {
        var url = RemoteControlActiveUrl;
        if (url == null)
        {
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Serilog.Log.Logger.Error(ex, "Could not open remote control URL {Url}", url);
        }
    }

    private void CopyRemoteControlUrl()
    {
        var url = RemoteControlActiveUrl;
        if (url == null)
        {
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(url);
            _snackbarService.EnqueueReplacingCurrent(Properties.Resources.REMOTE_CONTROL_URL_COPIED);
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            // the clipboard can be locked by another process
            Serilog.Log.Logger.Error(ex, "Could not copy remote control URL to clipboard");
        }
    }

    private void OpenMediaFolder()
    {
        var dialog = new CommonOpenFileDialog
        {
            IsFolderPicker = true,
            Title = Properties.Resources.MEDIA_FOLDER_BROWSE,
            InitialDirectory = GetMediaFolderBrowsingStart(),
            AddToMostRecentlyUsedList = false,
            DefaultDirectory = GetMediaFolderBrowsingStart(),
            EnsureFileExists = true,
            EnsurePathExists = true,
            EnsureReadOnly = false,
            EnsureValidNames = true,
            Multiselect = false,
            ShowPlacesList = true,
        };

        var result = dialog.ShowDialog();
        if (result == CommonFileDialogResult.Ok)
        {
            _recentlyUsedMediaFolders.Add(dialog.FileName);
            MediaFolder = dialog.FileName;
            OnPropertyChanged(nameof(RecentMediaFolders));
        }
    }

    private string? GetMediaFolderBrowsingStart()
    {
        if (!string.IsNullOrEmpty(MediaFolder) && Directory.Exists(MediaFolder))
        {
            return MediaFolder;
        }

        return null;
    }

    private void PurgeThumbnailCache() => _thumbnailService.ClearThumbCache();

    private void SetMediaWindowSize(Size size) => MediaWindowSize = size;

    private static LanguageItem[] GetSupportedLanguages()
    {
        var result = new List<LanguageItem>();

        var subFolders = Directory.GetDirectories(AppContext.BaseDirectory);

        foreach (var folder in subFolders)
        {
            if (!string.IsNullOrEmpty(folder))
            {
                try
                {
                    var c = new CultureInfo(Path.GetFileNameWithoutExtension(folder));
                    result.Add(new LanguageItem
                    {
                        LanguageId = c.Name,
                        LanguageName = c.EnglishName,
                    });
                }
                catch (CultureNotFoundException)
                {
                    // expected
                }
            }
        }

        // the native language
        var cNative = new CultureInfo(Path.GetFileNameWithoutExtension("en-GB"));
        result.Add(new LanguageItem
        {
            LanguageId = cNative.Name,
            LanguageName = cNative.EnglishName,
        });

        result.Sort((x, y) => string.CompareOrdinal(x.LanguageName, y.LanguageName));

        return result.ToArray();
    }

    private static MirrorHotKeyItem[] GetMirrorHotKeys()
    {
        var result = new List<MirrorHotKeyItem>();

        for (var ch = 'A'; ch <= 'Z'; ++ch)
        {
            result.Add(new MirrorHotKeyItem { Character = ch, Name = $"ALT+{ch}" });
        }

        return result.ToArray();
    }
}
