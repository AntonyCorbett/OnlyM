namespace OnlyMSlideManager.Services
{
    using System.Threading.Tasks;

    public interface IDialogService
    {
        Task<bool?> ShouldSaveDirtyDataAsync();

        bool IsDialogVisible();
    }
}
