namespace OnlyM
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Windows;
    using AutoUpdates;
    using CefSharp;
    using CefSharp.Wpf;
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
        private readonly string _appString = "OnlyMMeetingMedia";
        private Mutex _appMutex;
        private readonly bool _successCefSharp;

        public App()
        {
            DispatcherHelper.Initialize();
            MediaElement.FFmpegDirectory = FMpegFolderName;
            RegisterMappings();

            // pre-load the CefSharp assemblies otherwise 1st instantiation is too long.
            System.Reflection.Assembly.Load("CefSharp.Wpf");

            _successCefSharp = InitCef();
        }

        public static string FMpegFolderName { get; } = $"{AppDomain.CurrentDomain.BaseDirectory}\\FFmpeg";

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

            if (!_successCefSharp)
            {
                Log.Logger.Error("Could not initialise CefSharp");
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

        private bool InitCef()
        {
            //// refer here:
            //// https://github.com/cefsharp/CefSharp/blob/cefsharp/43/CefSharp.Example/CefExample.cs#L54

            var settings = new CefSettings
            {
                CachePath = FileUtils.GetBrowserCacheFolder(),
                LogFile = FileUtils.GetBrowserLogFilePath(),
                LogSeverity = LogSeverity.Info
            };

            settings.CefCommandLineArgs.Add("no-proxy-server", "1");
            settings.CefCommandLineArgs.Add("force-device-scale-factor", "1");

            // does this help?
            ////settings.SetOffScreenRenderingBestPerformanceArgs();

            //// this setting is automatically added. It means that if the user has
            //// Pepper Flash installed it will be detected and used.
            //// settings.CefCommandLineArgs.Add("enable-system-flash", "1"); 

            return Cef.Initialize(settings);
        }
    }
}
