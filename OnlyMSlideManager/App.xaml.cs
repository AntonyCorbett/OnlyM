using Microsoft.Extensions.DependencyInjection;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using OnlyM.CoreSys.Services.Snackbar;
using OnlyM.CoreSys.Services.UI;
using OnlyMSlideManager.Services;
using OnlyMSlideManager.Services.DragAndDrop;
using OnlyMSlideManager.Services.Options;
using OnlyMSlideManager.ViewModel;

namespace OnlyMSlideManager
{
    using System.IO;
    using System.Threading;
    using System.Windows;
    using OnlyMSlideManager.Helpers;
    using Serilog;

    public partial class App : Application
    {
        private readonly string _appString = "OnlyMSlideManagerTool";
        private Mutex _appMutex;

        public App()
        {
        }

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

        private void ConfigureServices()
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

        private void ConfigureLogger()
        {
            string logsDirectory = FileUtils.GetLogFolder();

#if DEBUG
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(Path.Combine(logsDirectory, "log-.txt"), retainedFileCountLimit: 28, rollingInterval: RollingInterval.Day)
                .CreateLogger();
#else
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(Path.Combine(logsDirectory, "log-.txt"), retainedFileCountLimit: 28, rollingInterval: RollingInterval.Day)
                .CreateLogger();
#endif

            Log.Logger.Information("==== Launched ====");
        }
    }
}