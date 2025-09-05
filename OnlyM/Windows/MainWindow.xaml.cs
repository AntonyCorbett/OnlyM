using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using OnlyM.Core.Services.Options;
using OnlyM.CoreSys.Services.Snackbar;
using OnlyM.CoreSys.WindowsPositioning;
using OnlyM.PubSubMessages;
using OnlyM.Services.DragAndDrop;
using OnlyM.Services.MediaChanging;
using OnlyM.Services.Pages;

namespace OnlyM.Windows;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
// ReSharper disable once UnusedMember.Global
public partial class MainWindow
{
    private readonly IActiveMediaItemsService? _activeMediaItemsService;
    private readonly ISnackbarService? _snackbarService;
    private static readonly bool IsDesignMode = DesignerProperties.GetIsInDesignMode(new DependencyObject());

    public MainWindow()
    {
        InitializeComponent();

        // Safely resolve and cache services (avoids repeated Ioc access during Closing).
        if (!IsDesignMode)
        {
            try
            {
                _activeMediaItemsService = Ioc.Default.GetService<IActiveMediaItemsService>();
                _snackbarService = Ioc.Default.GetService<ISnackbarService>();
            }
            catch (System.InvalidOperationException)
            {
                // IOC not configured (design time, early shutdown, or failed startup). Leave fields null.
            }
        }
    }

    protected override void OnSourceInitialized(System.EventArgs e)
    {
        AdjustMainWindowPositionAndSize();

        if (!IsDesignMode)
        {
            try
            {
                var pageService = Ioc.Default.GetService<IPageService>();
                if (pageService != null)
                {
                    pageService.ScrollViewer = MainScrollViewer;
                }
            }
            catch (System.InvalidOperationException)
            {
                // Ignore if IOC not ready (rare).
            }
        }
    }

    private void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (IsDesignMode)
        {
            return;
        }

        // If IOC failed to configure, allow normal close.
        if (_activeMediaItemsService == null)
        {
            return;
        }

        if (_activeMediaItemsService.Any())
        {
            // Prevent app closing when media is active.
            _snackbarService?.EnqueueWithOk(Properties.Resources.MEDIA_ACTIVE, Properties.Resources.OK);
            e.Cancel = true;
        }
        else
        {
            SaveWindowPos();
            WeakReferenceMessenger.Default.Send(new ShutDownMessage());
        }
    }

    private void AdjustMainWindowPositionAndSize()
    {
        if (IsDesignMode)
        {
            return;
        }

        try
        {
            var optionsService = Ioc.Default.GetService<IOptionsService>();
            if (!string.IsNullOrEmpty(optionsService?.AppWindowPlacement))
            {
                ResizeMode = WindowState == WindowState.Maximized
                    ? ResizeMode.NoResize
                    : ResizeMode.CanResizeWithGrip;

                this.SetPlacement(optionsService.AppWindowPlacement);
            }
        }
        catch (System.InvalidOperationException)
        {
            // Ignore if not configured.
        }
    }

    private void SaveWindowPos()
    {
        if (IsDesignMode)
        {
            return;
        }

        try
        {
            var optionsService = Ioc.Default.GetService<IOptionsService>();
            if (optionsService != null)
            {
                optionsService.AppWindowPlacement = this.GetPlacement();
                optionsService.Save();
            }
        }
        catch (System.InvalidOperationException)
        {
            // Ignore if not configured.
        }
    }

    private void OnPaste(object? sender, ExecutedRoutedEventArgs e)
    {
        if (IsDesignMode)
        {
            return;
        }

        try
        {
            var dragAndDropService = Ioc.Default.GetService<IDragAndDropService>();
            if (dragAndDropService != null)
            {
                dragAndDropService.Paste();
                e.Handled = true;
            }
        }
        catch (System.InvalidOperationException)
        {
            // Ignore if not configured.
        }
    }
}
