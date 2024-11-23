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
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(System.EventArgs e)
    {
        AdjustMainWindowPositionAndSize();

        var pageService = Ioc.Default.GetService<IPageService>();
        if (pageService != null)
        {
            pageService.ScrollViewer = MainScrollViewer;
        }
    }

    private void WindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var activeMediaService = Ioc.Default.GetService<IActiveMediaItemsService>();
        if (activeMediaService == null)
        {
            return;
        }

        if (activeMediaService.Any())
        {
            // prevent app closing when media is active.
            var snackbarService = Ioc.Default.GetService<ISnackbarService>();
            snackbarService?.EnqueueWithOk(Properties.Resources.MEDIA_ACTIVE, Properties.Resources.OK);
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
        var optionsService = Ioc.Default.GetService<IOptionsService>();
        if (!string.IsNullOrEmpty(optionsService?.AppWindowPlacement))
        {
            ResizeMode = WindowState == WindowState.Maximized
                ? ResizeMode.NoResize
                : ResizeMode.CanResizeWithGrip;

            this.SetPlacement(optionsService.AppWindowPlacement);
        }
    }

    private void SaveWindowPos()
    {
        var optionsService = Ioc.Default.GetService<IOptionsService>();
        if (optionsService != null)
        {
            optionsService.AppWindowPlacement = this.GetPlacement();
            optionsService.Save();
        }
    }

    private void OnPaste(object? sender, ExecutedRoutedEventArgs e)
    {
        var dragAndDropService = Ioc.Default.GetService<IDragAndDropService>();
        if (dragAndDropService != null)
        {
            dragAndDropService.Paste();
            e.Handled = true;
        }
    }
}
