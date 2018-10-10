namespace OnlyM.MediaElementAdaption
{
    using System;
    using System.Threading.Tasks;
    using System.Windows;
    using OnlyM.Core.Models;

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

        bool IsPaused { get; }

        Guid MediaItemId { get; set; }

        FrameworkElement FrameworkElement { get; }

        Task Play(Uri mediaPath, MediaClassification mediaClassification);

        Task Pause();

        Task Close();
        
        void UnsubscribeEvents();
    }
}
