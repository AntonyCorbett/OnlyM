using System;
using System.Threading.Tasks;

namespace OnlyM.Services.Dialogs;

public interface IDialogService
{
    Task<TimeSpan?> GetStartOffsetAsync(string mediaFileNameWithExtension, int maxStartTimeSeconds);

    bool IsDialogVisible();
}
