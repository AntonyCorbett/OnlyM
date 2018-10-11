namespace OnlyM.Services.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using MediaElementAdaption;
    using Models;

    internal interface IPageService
    {
        event EventHandler<NavigationEventArgs> NavigationEvent;

        event EventHandler MediaMonitorChangedEvent;

        event EventHandler<MediaEventArgs> MediaChangeEvent;

        event EventHandler<SlideTransitionEventArgs> SlideTransitionEvent;

        event EventHandler<PositionChangedEventArgs> MediaPositionChangedEvent;

        event EventHandler MediaWindowOpenedEvent;

        event EventHandler MediaWindowClosedEvent;

        event EventHandler<WindowVisibilityChangedEventArgs> MediaWindowVisibilityChanged;
        
        event EventHandler<MediaNearEndEventArgs> MediaNearEndEvent;

        bool AllowMediaWindowToClose { get; }

        string OperatorPageName { get; }

        string SettingsPageName { get; }

        bool ApplicationIsClosing { get; }

        bool IsMediaWindowVisible { get; }

        ScrollViewer ScrollViewer { get; set; }

        void GotoOperatorPage();

        void GotoSettingsPage();

        FrameworkElement GetPage(string pageName);
        
        void OpenMediaWindow(bool requiresVisibleWindow);
        
        Task StartMedia(MediaItem mediaItemToStart, IReadOnlyCollection<MediaItem> currentMediaItems, bool startFromPaused);

        Task StopMediaAsync(MediaItem mediaItem);

        Task PauseMediaAsync(MediaItem mediaItem);

        void CacheImageItem(MediaItem mediaItem);

        int GotoPreviousSlide();

        int GotoNextSlide();
    }
}
