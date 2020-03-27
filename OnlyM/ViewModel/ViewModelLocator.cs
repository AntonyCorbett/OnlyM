namespace OnlyM.ViewModel
{
    using CommonServiceLocator;
    using GalaSoft.MvvmLight.Ioc;
    using OnlyM.Core.Services.CommandLine;
    using OnlyM.Core.Services.Database;
    using OnlyM.Core.Services.Media;
    using OnlyM.Core.Services.Monitors;
    using OnlyM.Core.Services.Options;
    using OnlyM.CoreSys.Services.Snackbar;
    using OnlyM.Services.Dialogs;
    using OnlyM.Services.DragAndDrop;
    using OnlyM.Services.FrozenVideoItems;
    using OnlyM.Services.HiddenMediaItems;
    using OnlyM.Services.MediaChanging;
    using OnlyM.Services.Pages;
    using OnlyM.Services.PdfOptions;
    using OnlyM.Services.StartOffsetStorage;

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
            SimpleIoc.Default.Register<IPdfOptionsService, PdfOptionsService>();
            SimpleIoc.Default.Register<ICommandLineService, CommandLineService>();
            SimpleIoc.Default.Register<IActiveMediaItemsService, ActiveMediaItemsService>();
            SimpleIoc.Default.Register<IDialogService, DialogService>();
            SimpleIoc.Default.Register<IStartOffsetStorageService, StartOffsetStorageService>();

            // view models.
            SimpleIoc.Default.Register<StartOffsetViewModel>();

            SimpleIoc.Default.Register<MainViewModel>();
            SimpleIoc.Default.Register<MediaViewModel>();
            SimpleIoc.Default.Register<OperatorViewModel>();
            SimpleIoc.Default.Register<SettingsViewModel>();
        }

        public MainViewModel Main => ServiceLocator.Current.GetInstance<MainViewModel>();

        public MediaViewModel Media => ServiceLocator.Current.GetInstance<MediaViewModel>();

        public OperatorViewModel Operator => ServiceLocator.Current.GetInstance<OperatorViewModel>();

        public SettingsViewModel Settings => ServiceLocator.Current.GetInstance<SettingsViewModel>();

        public StartOffsetViewModel StartOffsetDialog => ServiceLocator.Current.GetInstance<StartOffsetViewModel>();

        public static void Cleanup()
        {
            // not required
        }
    }
}