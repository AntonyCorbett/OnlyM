using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Messaging;
using OnlyM.PubSubMessages;
using OnlyM.Services.DragAndDrop;
using OnlyM.ViewModel;

namespace OnlyM.Windows
{
    /// <summary>
    /// Interaction logic for OperatorPage.xaml
    /// </summary>
    public partial class OperatorPage : UserControl
    {
        public OperatorPage()
        {
            InitializeComponent();

            var dragAndDropService = Ioc.Default.GetService<IDragAndDropService>();
            dragAndDropService?.Init(this);
        }

        private void MirrorCheckBoxChecked(object? sender, RoutedEventArgs e)
        {
            HandleMirrorCheckBoxChanged(sender, true);
        }

        private void MirrorCheckBoxUnchecked(object? sender, RoutedEventArgs e)
        {
            HandleMirrorCheckBoxChanged(sender, false);
        }

        private static void HandleMirrorCheckBoxChanged(object? sender, bool isChecked)
        {
            if (sender is CheckBox cb)
            {
                var mediaItemGuid = (Guid)cb.Tag;
                WeakReferenceMessenger.Default.Send(new MirrorWindowMessage { MediaItemId = mediaItemGuid, UseMirror = isChecked });
            }
        }

        private void OnlyMOperatorPage_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            var vm = (OperatorViewModel?)DataContext;

            if (vm == null)
            {
                return;
            }

            if (e.NewSize.Width >= 500)
            {
                vm.ThumbnailColWidth = 180;
            }
            else if (e.NewSize.Width >= 400)
            {
                vm.ThumbnailColWidth = 100;
            }
            else
            {
                vm.ThumbnailColWidth = 46;
            }
        }
    }
}
