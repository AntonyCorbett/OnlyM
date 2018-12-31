namespace OnlyMSlideManager.Windows
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Shapes;
    using OnlyMSlideManager.ViewModel;

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
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
                vm.Drop(rect);
            }
        }
    }
}
