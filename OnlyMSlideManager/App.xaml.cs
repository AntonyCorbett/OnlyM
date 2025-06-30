using System.IO;
using System.Threading;
using System.Windows;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using OnlyM.CoreSys.Services.Snackbar;
using OnlyM.CoreSys.Services.UI;
using OnlyMSlideManager.Helpers;
using OnlyMSlideManager.Services;
using OnlyMSlideManager.Services.DragAndDrop;
using OnlyMSlideManager.Services.Options;
using OnlyMSlideManager.ViewModel;
using Serilog;

namespace OnlyMSlideManager;
#pragma warning disable CA1001
public partial class App
#pragma warning restore CA1001
{
    private readonly string _appString = "OnlyMSlideManagerTool";
    private Mutex? _appMutex;

    protected override void OnExit(ExitEventArgs e)
    {
        _appMutex?.Dispose();
        Log.Logger.Information("==== Exit ====");
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        if (AnotherInstanceRunning())
        {
            // allow
        }

        ConfigureLogger();
        ConfigureServices();
    }

    private static void ConfigureServices()
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddSingleton<MainViewModel>();
        serviceCollection.AddSingleton<ShouldSaveViewModel>();

        serviceCollection.AddSingleton<IDialogService, DialogService>();
        serviceCollection.AddSingleton<IDragAndDropServiceCustom, DragAndDropServiceCustom>();
        serviceCollection.AddSingleton<ISnackbarService, SnackbarService>();
        serviceCollection.AddSingleton<IUserInterfaceService, UserInterfaceService>();
        serviceCollection.AddSingleton<IOptionsService, OptionsService>();

        var serviceProvider = serviceCollection.BuildServiceProvider();
        Ioc.Default.ConfigureServices(serviceProvider);
    }

    private bool AnotherInstanceRunning()
    {
        _appMutex = new Mutex(true, _appString, out var newInstance);
        return !newInstance;
    }

    private static void ConfigureLogger()
    {
        var logsDirectory = FileUtils.GetLogFolder();

#pragma warning disable CA1305 // Specify IFormatProvider
        var config = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#else
                .MinimumLevel.Information()
#endif
            .WriteTo.File(
                Path.Combine(logsDirectory, "log-.txt"),
                retainedFileCountLimit: 28,
                rollingInterval: RollingInterval.Day);
#pragma warning restore CA1305 // Specify IFormatProvider

        Log.Logger = config.CreateLogger();

        Log.Logger.Information("==== Launched ====");
    }
}
