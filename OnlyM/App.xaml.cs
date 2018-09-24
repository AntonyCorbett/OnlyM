namespace OnlyM
{
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Windows;
    using AutoUpdates;
    using Core.Models;
    using Core.Services.Options;
    using Core.Utils;
    using GalaSoft.MvvmLight.Threading;
    using Models;
    using Serilog;
    using Unosquare.FFME;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "Singleton")]
    public partial class App : Application
    {
        public static string FMpegFolderName { get; } = "FFmpeg";
        private readonly string _appString = "OnlyMMeetingMedia";
        private Mutex _appMutex;

        public App()
        {
            DispatcherHelper.Initialize();
            MediaElement.FFmpegDirectory = FMpegFolderName;
            RegisterMappings();
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
                Shutdown();
            }
            else
            {
                ConfigureLogger();
            }

            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;
        }

        private void ConfigureLogger()
        {
            string logsDirectory = FileUtils.GetLogFolder();

            Log.Logger = new LoggerConfiguration()
               .MinimumLevel.ControlledBy(LogLevelSwitchService.LevelSwitch)
               .WriteTo.RollingFile(Path.Combine(logsDirectory, "log-{Date}.txt"), retainedFileCountLimit: 28)
#if DEBUG
               .WriteTo.Console()
#endif
               .CreateLogger();

            Log.Logger.Information("==== Launched ====");
            Log.Logger.Information($"Version {VersionDetection.GetCurrentVersion()}");
        }

        private bool AnotherInstanceRunning()
        {
            _appMutex = new Mutex(true, _appString, out var newInstance);
            return !newInstance;
        }

        private void RegisterMappings()
        {
            AutoMapper.Mapper.Initialize(cfg => cfg.CreateMap<SystemMonitor, MonitorItem>());
        }
    }
}
