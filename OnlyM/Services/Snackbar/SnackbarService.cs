namespace OnlyM.Services.Snackbar
{
    using System;
    using MaterialDesignThemes.Wpf;

    // ReSharper disable once ClassNeverInstantiated.Global
    internal class SnackbarService : ISnackbarService
    {
        public ISnackbarMessageQueue TheSnackbarMessageQueue { get; } = new SnackbarMessageQueue(TimeSpan.FromSeconds(4));

        public void Enqueue(object content, object actionContent, Action actionHandler)
        {
            TheSnackbarMessageQueue.Enqueue(content, actionContent, actionHandler);
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
