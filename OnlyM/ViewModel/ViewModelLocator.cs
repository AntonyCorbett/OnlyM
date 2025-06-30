using CommunityToolkit.Mvvm.DependencyInjection;

namespace OnlyM.ViewModel;

/// <summary>
/// This class contains static references to all the view models in the
/// application and provides an entry point for the bindings.
/// </summary>
internal sealed class ViewModelLocator
{
#pragma warning disable CA1822 // Mark members as static
    // ReSharper disable once MemberCanBeMadeStatic.Global
    public MainViewModel? Main => Ioc.Default.GetService<MainViewModel>();

    // ReSharper disable once MemberCanBeMadeStatic.Global
    public MediaViewModel? Media => Ioc.Default.GetService<MediaViewModel>();

    // ReSharper disable once MemberCanBeMadeStatic.Global
    public OperatorViewModel? Operator => Ioc.Default.GetService<OperatorViewModel>();

    // ReSharper disable once MemberCanBeMadeStatic.Global
    public SettingsViewModel? Settings => Ioc.Default.GetService<SettingsViewModel>();

    // ReSharper disable once MemberCanBeMadeStatic.Global
    public StartOffsetViewModel? StartOffsetDialog => Ioc.Default.GetService<StartOffsetViewModel>();
#pragma warning restore CA1822 // Mark members as static
}
