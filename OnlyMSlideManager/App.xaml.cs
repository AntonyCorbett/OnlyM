namespace OnlyMSlideManager
{
    using System.IO;
    using System.Windows;
    using GalaSoft.MvvmLight.Threading;
    using OnlyMSlideManager.Helpers;
    using Serilog;

    public partial class App : Application
    {
        public App()
        {
            DispatcherHelper.Initialize();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Logger.Information("==== Exit ====");
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            ConfigureLogger();
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
