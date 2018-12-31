namespace OnlyMSlideManager.ViewModel
{
    using CommonServiceLocator;
    using GalaSoft.MvvmLight;
    using GalaSoft.MvvmLight.Ioc;
    using OnlyMSlideManager.Services;
    using OnlyMSlideManager.Services.DragAndDrop;

    internal class ViewModelLocator
    {
        public ViewModelLocator()
        {
            ServiceLocator.SetLocatorProvider(() => SimpleIoc.Default);
            
            SimpleIoc.Default.Register<MainViewModel>();
            SimpleIoc.Default.Register<ShouldSaveViewModel>();

            SimpleIoc.Default.Register<IDialogService, DialogService>();
            SimpleIoc.Default.Register<IDragAndDropServiceCustom, DragAndDropServiceCustom>();
        }

        public MainViewModel Main => ServiceLocator.Current.GetInstance<MainViewModel>();

        public ShouldSaveViewModel ShouldSaveDialog => ServiceLocator.Current.GetInstance<ShouldSaveViewModel>();

        public static void Cleanup()
        {
            // TODO Clear the ViewModels
        }
    }
}