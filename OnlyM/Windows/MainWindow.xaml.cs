namespace OnlyM.Windows
{
    using System.Windows;
    using System.Windows.Input;
    using CommonServiceLocator;
    using GalaSoft.MvvmLight.Messaging;
    using OnlyM.Core.Services.Options;
    using OnlyM.CoreSys.Services.Snackbar;
    using OnlyM.CoreSys.WindowsPositioning;
    using OnlyM.PubSubMessages;
    using OnlyM.Services.DragAndDrop;
    using OnlyM.Services.MediaChanging;
    using OnlyM.Services.Pages;
    
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
                snackbarService.EnqueueWithOk(Properties.Resources.MEDIA_ACTIVE, Properties.Resources.OK);
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
            if (!string.IsNullOrEmpty(optionsService.AppWindowPlacement))
            {
                ResizeMode = WindowState == WindowState.Maximized
                    ? ResizeMode.NoResize
                    : ResizeMode.CanResizeWithGrip;

                this.SetPlacement(optionsService.AppWindowPlacement);
            }
        }

        private void SaveWindowPos()
        {
            var optionsService = ServiceLocator.Current.GetInstance<IOptionsService>();
            optionsService.AppWindowPlacement = this.GetPlacement();
            optionsService.Save();
        }

        private void OnPaste(object sender, ExecutedRoutedEventArgs e)
        {
            var dragAndDropService = ServiceLocator.Current.GetInstance<IDragAndDropService>();
            dragAndDropService.Paste();
            e.Handled = true;
        }
    }
}
