using OnlyM.ViewModel;

namespace OnlyM.Services.Dialogs
{
    using System;
    using System.Threading.Tasks;
    using MaterialDesignThemes.Wpf;
    using OnlyM.Dialogs;

    internal class DialogService : IDialogService
    {
        private bool _isDialogVisible;

        public async Task<TimeSpan?> GetStartOffsetAsync()
        {
            _isDialogVisible = true;

            var dialog = new StartOffsetDialog();
            var dc = (StartOffsetViewModel)dialog.DataContext;

            // dirty data.
            await DialogHost.Show(
                    dialog,
                    (object sender, DialogClosingEventArgs args) => { _isDialogVisible = false; })
                .ConfigureAwait(false);

            return dc.Result;
        }

        public bool IsDialogVisible()
        {
            return _isDialogVisible;
        }
    }
}
