namespace OnlyM.MediaElementAdaption
{
    using System;
    using System.Threading.Tasks;

    internal interface IMediaElement
    {
        event EventHandler<System.Windows.RoutedEventArgs> MediaOpened;

        event EventHandler<System.Windows.RoutedEventArgs> MediaClosed;

        event EventHandler<System.Windows.RoutedEventArgs> MediaEnded;

        event EventHandler<System.Windows.ExceptionRoutedEventArgs> MediaFailed;

        event EventHandler<RenderSubtitlesEventArgs> RenderingSubtitles;

        event EventHandler<PositionChangedEventArgs> PositionChanged;

        event EventHandler<LogMessageEventArgs> MessageLogged;

        TimeSpan Position { get; set; }

        Task Play();

        Task Pause();

        Task Close();

        Uri Source { get; set; }

        bool IsPaused { get; }
    }
}
