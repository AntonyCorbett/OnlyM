namespace OnlyMSlideManager.Services
{
    using System.Threading.Tasks;
    using MaterialDesignThemes.Wpf;
    using OnlyMSlideManager.Dialogs;
    using OnlyMSlideManager.ViewModel;

    // ReSharper disable once ClassNeverInstantiated.Global
    internal class DialogService : IDialogService
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
