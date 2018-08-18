namespace OnlyM.MediaElementAdaption
{
    using System;
    using System.Threading.Tasks;
    using System.Windows;

    internal interface IMediaElement
    {
        event EventHandler<RoutedEventArgs> MediaOpened;

        event EventHandler<RoutedEventArgs> MediaClosed;

        event EventHandler<RoutedEventArgs> MediaEnded;

        event EventHandler<ExceptionRoutedEventArgs> MediaFailed;

        event EventHandler<RenderSubtitlesEventArgs> RenderingSubtitles;

        event EventHandler<PositionChangedEventArgs> PositionChanged;

        event EventHandler<LogMessageEventArgs> MessageLogged;

        TimeSpan Position { get; set; }

        Duration NaturalDuration { get; }
    
        Task Play(Uri mediaPath);

        Task Pause();

        Task Close();
        
        bool IsPaused { get; }

        FrameworkElement FrameworkElement { get; }

        Guid MediaItemId { get; set; }
    }
}
