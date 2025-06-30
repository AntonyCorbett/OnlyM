using System;
using System.Threading.Tasks;
using MaterialDesignThemes.Wpf;
using OnlyM.Dialogs;
using OnlyM.ViewModel;

namespace OnlyM.Services.Dialogs;

internal sealed class DialogService : IDialogService
{
    private bool _isDialogVisible;

    public async Task<TimeSpan?> GetStartOffsetAsync(string mediaFileNameWithExtension, int maxStartTimeSeconds)
    {
        _isDialogVisible = true;

        var dialog = new StartOffsetDialog();
        var dc = (StartOffsetViewModel)dialog.DataContext;

        dc.Init(mediaFileNameWithExtension, maxStartTimeSeconds);

        await DialogHost.Show(
            dialog,
            (object _, DialogClosingEventArgs args) => _isDialogVisible = false);

        return dc.Result;
    }

    public bool IsDialogVisible() => _isDialogVisible;
}
