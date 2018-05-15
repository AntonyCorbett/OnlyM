namespace OnlyM.ViewModel
{
    using CommonServiceLocator;
    using Core.Services.Media;
    using Core.Services.Monitors;
    using Core.Services.Options;
    using GalaSoft.MvvmLight.Ioc;
    using Services;
    using Services.Pages;

    /// <summary>
    /// This class contains static references to all the view models in the
    /// application and provides an entry point for the bindings.
    /// </summary>
    internal class ViewModelLocator
    {
        /// <summary>
        /// Initializes a new instance of the ViewModelLocator class.
        /// </summary>
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
            // TODO Clear the ViewModels
        }
    }
}