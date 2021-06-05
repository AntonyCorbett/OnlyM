using Microsoft.Toolkit.Mvvm.DependencyInjection;

namespace OnlyMSlideManager.ViewModel
{
    internal class ViewModelLocator
    {
        public MainViewModel Main => Ioc.Default.GetService<MainViewModel>();

        public ShouldSaveViewModel ShouldSaveDialog => Ioc.Default.GetService<ShouldSaveViewModel>();
    }
}