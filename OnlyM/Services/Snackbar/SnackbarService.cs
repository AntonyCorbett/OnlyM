namespace OnlyM.Services.Snackbar
{
    using System;
    using MaterialDesignThemes.Wpf;

    // ReSharper disable once ClassNeverInstantiated.Global
    public class SnackbarService : ISnackbarService
    {
        public ISnackbarMessageQueue TheSnackbarMessageQueue { get; } = new SnackbarMessageQueue(TimeSpan.FromSeconds(4));

        public void Enqueue(object content, object actionContent, Action actionHandler, bool promote = false)
        {
            TheSnackbarMessageQueue.Enqueue(content, actionContent, actionHandler, promote);
        }

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

        public void Enqueue(object content)
        {
            TheSnackbarMessageQueue.Enqueue(content);
        }

        public void EnqueueWithOk(object content)
        {
            TheSnackbarMessageQueue.Enqueue(content, Properties.Resources.OK, () => { });
        }
    }
}
