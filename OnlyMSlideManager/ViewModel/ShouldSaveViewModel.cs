using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;

namespace OnlyMSlideManager.ViewModel;

internal sealed class ShouldSaveViewModel
{
    public ShouldSaveViewModel()
    {
        YesCommand = new RelayCommand(Yes);
        NoCommand = new RelayCommand(No);
        CancelCommand = new RelayCommand(Cancel);
    }

    public bool? Result { get; private set; }

    public RelayCommand YesCommand { get; }

    public RelayCommand NoCommand { get; }

    public RelayCommand CancelCommand { get; }

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
