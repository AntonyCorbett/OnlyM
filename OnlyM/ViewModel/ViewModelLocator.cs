using CommunityToolkit.Mvvm.DependencyInjection;

namespace OnlyM.ViewModel
{
    /// <summary>
    /// This class contains static references to all the view models in the
    /// application and provides an entry point for the bindings.
    /// </summary>
    internal class ViewModelLocator
    {
#pragma warning disable CA1822 // Mark members as static
        public MainViewModel? Main => Ioc.Default.GetService<MainViewModel>();

        public MediaViewModel? Media => Ioc.Default.GetService<MediaViewModel>();

        public OperatorViewModel? Operator => Ioc.Default.GetService<OperatorViewModel>();

        public SettingsViewModel? Settings => Ioc.Default.GetService<SettingsViewModel>();

        public StartOffsetViewModel? StartOffsetDialog => Ioc.Default.GetService<StartOffsetViewModel>();
#pragma warning restore CA1822 // Mark members as static
    }
}
