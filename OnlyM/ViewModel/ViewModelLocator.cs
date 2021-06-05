namespace OnlyM.ViewModel
{
    using CommonServiceLocator;
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