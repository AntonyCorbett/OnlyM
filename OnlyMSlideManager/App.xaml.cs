namespace OnlyMSlideManager
{
    using System.IO;
    using System.Threading;
    using System.Windows;
    using GalaSoft.MvvmLight.Threading;
    using OnlyMSlideManager.Helpers;
    using Serilog;

    public partial class App : Application
    {
        private readonly string _appString = "OnlyMSlideManagerTool";
        private Mutex _appMutex;
        
        public App()
        {
            DispatcherHelper.Initialize();
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
        }

        private bool AnotherInstanceRunning()
        {
            _appMutex = new Mutex(true, _appString, out var newInstance);
            return !newInstance;
        }

        private void ConfigureLogger()
        {
            string logsDirectory = FileUtils.GetLogFolder();

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.RollingFile(Path.Combine(logsDirectory, "log-{Date}.txt"), retainedFileCountLimit: 28)
                .CreateLogger();

            Log.Logger.Information("==== Launched ====");
        }
    }
}
