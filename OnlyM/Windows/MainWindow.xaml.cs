namespace OnlyM.Windows
{
    using System.Windows;
    using System.Windows.Input;
    using CommonServiceLocator;
    using Core.Services.Options;
    using GalaSoft.MvvmLight.Messaging;
    using MaterialDesignThemes.Wpf;
    using PubSubMessages;
    using Services.MediaChanging;
    using Services.Pages;
    using Services.Snackbar;
    using WindowsPositioning;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var pageService = ServiceLocator.Current.GetInstance<IPageService>();
            pageService.ScrollViewer = MainScrollViewer;
        }

        protected override void OnSourceInitialized(System.EventArgs e)
        {
            AdjustMainWindowPositionAndSize();
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var activeMediaService = ServiceLocator.Current.GetInstance<IActiveMediaItemsService>();
            if (activeMediaService.Any())
            {
                // prevent app closing when media is active.
                var snackbarService = ServiceLocator.Current.GetInstance<ISnackbarService>();
                snackbarService.Enqueue(Properties.Resources.MEDIA_ACTIVE);
                e.Cancel = true;
            }
            else
            {
                SaveWindowPos();
                Messenger.Default.Send(new ShutDownMessage());
            }
        }

        private void AdjustMainWindowPositionAndSize()
        {
            var optionsService = ServiceLocator.Current.GetInstance<IOptionsService>();
            if (!string.IsNullOrEmpty(optionsService.Options.AppWindowPlacement))
            {
                ResizeMode = WindowState == WindowState.Maximized
                    ? ResizeMode.NoResize
                    : ResizeMode.CanResizeWithGrip;

                this.SetPlacement(optionsService.Options.AppWindowPlacement);
            }
        }

        private void SaveWindowPos()
        {
            var optionsService = ServiceLocator.Current.GetInstance<IOptionsService>();
            optionsService.Options.AppWindowPlacement = this.GetPlacement();
            optionsService.Save();
        }

        private void OnMouseDoubleClickTitleBar(object sender, MouseButtonEventArgs e)
        {
            MaximizeOrRestore();
        }

        private void MaximizeOrRestore()
        {
            if (WindowState == WindowState.Maximized)
            {
                ResizeMode = ResizeMode.CanResizeWithGrip;
                WindowState = WindowState.Normal;
                MaxRestoreBtn.Content = new PackIcon { Kind = PackIconKind.WindowMaximize };
            }
            else
            {
                ResizeMode = ResizeMode.NoResize;
                WindowState = WindowState.Maximized;
                MaxRestoreBtn.Content = new PackIcon { Kind = PackIconKind.WindowRestore };
            }
        }

        private void OnMouseLeftButtonDownTitleBar(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void MinimizeClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeRestoreClick(object sender, RoutedEventArgs e)
        {
            MaximizeOrRestore();
        }

        private void CloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
