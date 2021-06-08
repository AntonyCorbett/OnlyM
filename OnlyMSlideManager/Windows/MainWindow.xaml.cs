using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Messaging;
using OnlyM.CoreSys.WindowsPositioning;
using OnlyMSlideManager.PubSubMessages;
using OnlyMSlideManager.Services.Options;
using OnlyMSlideManager.ViewModel;

namespace OnlyMSlideManager.Windows
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            WeakReferenceMessenger.Default.Register<CloseAppMessage>(this, OnCloseAppMessage);
        }

        protected override void OnSourceInitialized(System.EventArgs e)
        {
            AdjustMainWindowPositionAndSize();
            base.OnSourceInitialized(e);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            SaveWindowPos();
            base.OnClosing(e);
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

        private void OnCloseAppMessage(object recipient, CloseAppMessage message)
        {
            Close();
        }
    }
}
