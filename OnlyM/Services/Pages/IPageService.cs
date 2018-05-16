namespace OnlyM.Services.Pages
{
    using System;
    using System.Threading.Tasks;
    using System.Windows;
    using Models;
    using Unosquare.FFME.Events;

    internal interface IPageService
    {
        event EventHandler<NavigationEventArgs> NavigationEvent;

        event EventHandler MediaMonitorChangedEvent;

        event EventHandler<MediaEventArgs> MediaChangeEvent;

        event EventHandler<PositionChangedRoutedEventArgs> MediaPositionChangedEvent;

        string OperatorPageName { get; }

        string SettingsPageName { get; }

        void GotoOperatorPage();

        void GotoSettingsPage();

        FrameworkElement GetPage(string pageName);
        
        void OpenMediaWindow();

        bool ApplicationIsClosing { get; }

        bool IsMediaWindowVisible { get; }
        
        void UpdateMediaMonitor();

        void StartMedia(MediaItem mediaItemToStart, MediaItem currentMediaItem);

        Task StopMediaAsync(MediaItem mediaItem);

        void CacheImageItem(MediaItem mediaItem);

        Guid CurrentMediaId { get; }

        bool IsMediaItemActive { get; }
    }
}
