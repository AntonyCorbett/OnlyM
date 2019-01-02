namespace OnlyM.ViewModel
{
    using CommonServiceLocator;
    using Core.Services.CommandLine;
    using Core.Services.Database;
    using Core.Services.Media;
    using Core.Services.Monitors;
    using Core.Services.Options;
    using GalaSoft.MvvmLight.Ioc;
    using OnlyM.CoreSys.Services.Snackbar;
    using Services.DragAndDrop;
    using Services.FrozenVideoItems;
    using Services.HiddenMediaItems;
    using Services.MediaChanging;
    using Services.Pages;
    
    /// <summary>
    /// This class contains static references to all the view models in the
    /// application and provides an entry point for the bindings.
    /// </summary>
    internal class ViewModelLocator
    {
        public ViewModelLocator()
        {
            ServiceLocator.SetLocatorProvider(() => SimpleIoc.Default);

            // services.
            SimpleIoc.Default.Register<IPageService, PageService>();
            SimpleIoc.Default.Register<IMediaProviderService, MediaProviderService>();
            SimpleIoc.Default.Register<IThumbnailService, ThumbnailService>();
            SimpleIoc.Default.Register<IMonitorsService, MonitorsService>();
            SimpleIoc.Default.Register<IOptionsService, OptionsService>();
            SimpleIoc.Default.Register<ILogLevelSwitchService, LogLevelSwitchService>();
            SimpleIoc.Default.Register<IFolderWatcherService, FolderWatcherService>();
            SimpleIoc.Default.Register<IDatabaseService, DatabaseService>();
            SimpleIoc.Default.Register<IMediaMetaDataService, MediaMetaDataService>();
            SimpleIoc.Default.Register<IDragAndDropService, DragAndDropService>();
            SimpleIoc.Default.Register<ISnackbarService, SnackbarService>();
            SimpleIoc.Default.Register<IMediaStatusChangingService, MediaStatusChangingService>();
            SimpleIoc.Default.Register<IHiddenMediaItemsService, HiddenMediaItemsService>();
            SimpleIoc.Default.Register<IFrozenVideosService, FrozenVideosService>();
            SimpleIoc.Default.Register<ICommandLineService, CommandLineService>();
            SimpleIoc.Default.Register<IActiveMediaItemsService, ActiveMediaItemsService>();

            // view models.
            SimpleIoc.Default.Register<MainViewModel>();
            SimpleIoc.Default.Register<MediaViewModel>();
            SimpleIoc.Default.Register<OperatorViewModel>();
            SimpleIoc.Default.Register<SettingsViewModel>();
        }

        public MainViewModel Main => ServiceLocator.Current.GetInstance<MainViewModel>();

        public MediaViewModel Media => ServiceLocator.Current.GetInstance<MediaViewModel>();

        public OperatorViewModel Operator => ServiceLocator.Current.GetInstance<OperatorViewModel>();

        public SettingsViewModel Settings => ServiceLocator.Current.GetInstance<SettingsViewModel>();

        public static void Cleanup()
        {
        }
    }
}