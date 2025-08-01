﻿using System.Threading.Tasks;
using MaterialDesignThemes.Wpf;
using OnlyMSlideManager.Dialogs;
using OnlyMSlideManager.ViewModel;

namespace OnlyMSlideManager.Services;

// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class DialogService : IDialogService
{
    private bool _isDialogVisible;

    public async Task<bool?> ShouldSaveDirtyDataAsync()
    {
        _isDialogVisible = true;

        var dialog = new ShouldSaveDialog();
        var dc = (ShouldSaveViewModel)dialog.DataContext;

        // dirty data.
        await DialogHost.Show(
                dialog,
                (object _, DialogClosingEventArgs _) => _isDialogVisible = false)
            .ConfigureAwait(false);

        return dc.Result;
    }

    public bool IsDialogVisible() => _isDialogVisible;
}
