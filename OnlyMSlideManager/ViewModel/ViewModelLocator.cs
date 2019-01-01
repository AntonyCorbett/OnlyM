namespace OnlyMSlideManager.ViewModel
{
    using CommonServiceLocator;
    using GalaSoft.MvvmLight;
    using GalaSoft.MvvmLight.Ioc;
    using OnlyMSlideManager.Services;
    using OnlyMSlideManager.Services.DragAndDrop;
    using OnlyMSlideManager.Services.Snackbar;
    using OnlyMSlideManager.Services.UI;

    internal class ViewModelLocator
    {
        public ViewModelLocator()
        {
            ServiceLocator.SetLocatorProvider(() => SimpleIoc.Default);
            
            SimpleIoc.Default.Register<MainViewModel>();
            SimpleIoc.Default.Register<ShouldSaveViewModel>();

            SimpleIoc.Default.Register<IDialogService, DialogService>();
            SimpleIoc.Default.Register<IDragAndDropServiceCustom, DragAndDropServiceCustom>();
            SimpleIoc.Default.Register<ISnackbarService, SnackbarService>();
            SimpleIoc.Default.Register<IUserInterfaceService, UserInterfaceService>();
        }

        public MainViewModel Main => ServiceLocator.Current.GetInstance<MainViewModel>();

        public ShouldSaveViewModel ShouldSaveDialog => ServiceLocator.Current.GetInstance<ShouldSaveViewModel>();

        public static void Cleanup()
        {
            // TODO Clear the ViewModels
        }
    }
}