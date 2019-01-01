namespace OnlyMSlideManager.Windows
{
    using System.ComponentModel;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Shapes;
    using CommonServiceLocator;
    using GalaSoft.MvvmLight.Messaging;
    using OnlyMSlideManager.PubSubMessages;
    using OnlyMSlideManager.Services.Options;
    using OnlyMSlideManager.Services.WindowsPositioning;
    using OnlyMSlideManager.ViewModel;

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            Messenger.Default.Register<CloseAppMessage>(this, OnCloseAppMessage);
        }

        protected override void OnSourceInitialized(System.EventArgs e)
        {
            AdjustMainWindowPositionAndSize();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            SaveWindowPos();
        }

        private void SaveWindowPos()
        {
            var optionsService = ServiceLocator.Current.GetInstance<IOptionsService>();
            optionsService.AppWindowPlacement = this.GetPlacement();
            optionsService.Save();
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

        private void DragSourcePreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Control card)
            {
                var vm = (MainViewModel)DataContext;
                vm.DragSourcePreviewMouseDown(card, e.GetPosition(null));
            }
        }

        private void DragSourcePreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var position = e.GetPosition(null);

                var vm = (MainViewModel)DataContext;
                vm.DragSourcePreviewMouseMove(position);
            }
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            if (sender is Rectangle rect)
            {
                var vm = (MainViewModel)DataContext;
                vm.Drop(rect, e);
            }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (sender is Rectangle rect)
            {
                var vm = (MainViewModel)DataContext;
                vm.DragEnter(rect, e);
            }
        }

        private void OnCloseAppMessage(CloseAppMessage message)
        {
            Close();
        }
    }
}
