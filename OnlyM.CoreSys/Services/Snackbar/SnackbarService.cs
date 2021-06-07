using System;
using MaterialDesignThemes.Wpf;

namespace OnlyM.CoreSys.Services.Snackbar
{
    public sealed class SnackbarService : ISnackbarService, IDisposable
    {
        public ISnackbarMessageQueue TheSnackbarMessageQueue { get; } = new SnackbarMessageQueue(TimeSpan.FromSeconds(4));

        public void Enqueue(object content, object actionContent, Action actionHandler, bool promote = false) 
            => TheSnackbarMessageQueue.Enqueue(content, actionContent, actionHandler, promote);
        
        public void Enqueue(
            object content,
            object actionContent,
            Action<object> actionHandler,
            object actionArgument,
            bool promote,
            bool neverConsiderToBeDuplicate)
        {
            TheSnackbarMessageQueue.Enqueue(
                content,
                actionContent,
                actionHandler,
                actionArgument,
                promote,
                neverConsiderToBeDuplicate);
        }

        public void Enqueue(object content) => TheSnackbarMessageQueue.Enqueue(content);
        
        public void EnqueueWithOk(object content, string okText) => TheSnackbarMessageQueue.Enqueue(content, okText, () => { });
        
        public void Dispose() => ((SnackbarMessageQueue)TheSnackbarMessageQueue)?.Dispose();
    }
}
