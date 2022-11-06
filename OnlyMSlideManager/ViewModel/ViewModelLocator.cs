using CommunityToolkit.Mvvm.DependencyInjection;

namespace OnlyMSlideManager.ViewModel
{
    internal class ViewModelLocator
    {
#pragma warning disable CA1822 // Mark members as static
        public MainViewModel? Main => Ioc.Default.GetService<MainViewModel>();

        public ShouldSaveViewModel? ShouldSaveDialog => Ioc.Default.GetService<ShouldSaveViewModel>();
#pragma warning restore CA1822 // Mark members as static
    }
}
