using System.Threading.Tasks;

namespace OnlyMSlideManager.Services
{
    public interface IDialogService
    {
        Task<bool?> ShouldSaveDirtyDataAsync();

        bool IsDialogVisible();
    }
}
