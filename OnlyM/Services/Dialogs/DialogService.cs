namespace OnlyM.Services.Dialogs
{
    using System;
    using System.Threading.Tasks;
    using MaterialDesignThemes.Wpf;
    using OnlyM.Dialogs;
    using OnlyM.ViewModel;

    internal class DialogService : IDialogService
    {
        private bool _isDialogVisible;

        public async Task<TimeSpan?> GetStartOffsetAsync(TimeSpan maxStartTime)
        {
            _isDialogVisible = true;

            var dialog = new StartOffsetDialog();
            var dc = (StartOffsetViewModel)dialog.DataContext;

            dc.MaxStartTime = maxStartTime;
            dc.ChosenHours = 0;
            dc.ChosenMinutes = 0;
            dc.ChosenSeconds = 0;

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
