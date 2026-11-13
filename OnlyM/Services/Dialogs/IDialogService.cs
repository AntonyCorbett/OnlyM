using System;
using System.Threading.Tasks;

namespace OnlyM.Services.Dialogs;

public interface IDialogService
{
    Task<TimeSpan?> GetStartOffsetAsync(string mediaFileNameWithExtension, int maxStartTimeSeconds);

    /// <summary>
    /// Shows a yes/no confirmation dialog.
    /// </summary>
    /// <returns>True if the user confirmed.</returns>
    Task<bool> ConfirmAsync(string message, string? detail, string confirmText);

    bool IsDialogVisible();
}
