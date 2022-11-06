using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;

namespace OnlyMSlideManager.ViewModel
{
    internal class ShouldSaveViewModel
    {
        public ShouldSaveViewModel()
        {
            YesCommand = new RelayCommand(Yes);
            NoCommand = new RelayCommand(No);
            CancelCommand = new RelayCommand(Cancel);
        }

        public bool? Result { get; private set; }

        public RelayCommand YesCommand { get; set; }

        public RelayCommand NoCommand { get; set; }

        public RelayCommand CancelCommand { get; set; }

        private void Cancel()
        {
            Result = null;
            DialogHost.CloseDialogCommand.Execute(null, null);
        }

        private void No()
        {
            Result = false;
            DialogHost.CloseDialogCommand.Execute(null, null);
        }

        private void Yes()
        {
            Result = true;
            DialogHost.CloseDialogCommand.Execute(null, null);
        }
    }
}
