namespace OnlyM.Services.Snackbar
{
    using System;
    using MaterialDesignThemes.Wpf;

    internal interface ISnackbarService
    {
        ISnackbarMessageQueue TheSnackbarMessageQueue { get; }

        void Enqueue(object content, object actionContent, Action actionHandler);

        void Enqueue(object content);

        void EnqueueWithOk(object content);
    }
}
