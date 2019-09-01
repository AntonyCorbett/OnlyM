namespace OnlyM.Services.Dialogs
{
    using System;
    using System.Threading.Tasks;

    public interface IDialogService
    {
        Task<TimeSpan?> GetStartOffsetAsync();

        bool IsDialogVisible();
    }
}
