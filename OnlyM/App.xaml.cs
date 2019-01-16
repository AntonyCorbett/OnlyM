namespace OnlyM
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Windows;
    using System.Windows.Threading;
    using AutoUpdates;
    using CefSharp;
    using CefSharp.Wpf;
    using Core.Models;
    using Core.Services.Options;
    using Core.Utils;
    using GalaSoft.MvvmLight.Threading;
    using Models;
    using OnlyM.Services.WebBrowser;
    using Serilog;
    using Unosquare.FFME;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "Singleton")]
    public partial class App : Application
    {
        private readonly string _appString = "OnlyMMeetingMedia";
        private readonly bool _successCefSharp;
        private Mutex _appMutex;

        public App()
        {
            try
            {
                DispatcherHelper.Initialize();
                MediaElement.FFmpegDirectory = FMpegFolderName;
                RegisterMappings();

                // pre-load the CefSharp assemblies otherwise 1st instantiation is too long.
                System.Reflection.Assembly.Load("CefSharp.Wpf");

                _successCefSharp = InitCef();
            }
            catch (Exception ex)
            {
                AddEventLogEntry(ex.Message);
                Current.Shutdown();
            }
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

                Current.DispatcherUnhandledException += CurrentDispatcherUnhandledException;
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
            
            //// this setting is automatically added. It means that if the user has
            //// Pepper Flash installed it will be detected and used.
            //// settings.CefCommandLineArgs.Add("enable-system-flash", "1");

            // custom pdf scheme...
            settings.RegisterScheme(new CefCustomScheme
            {
                SchemeName = PdfSchemeHandlerFactory.SchemeName,
                SchemeHandlerFactory = new PdfSchemeHandlerFactory(),
                IsCSPBypassing = true
            });

            // prevents orphaned CelSharp.BrowserSubprocess.exe instances
            // caused by an OnlyM crash.
            CefSharpSettings.SubprocessExitIfParentProcessClosed = true;

            return Cef.Initialize(settings);
        }

        private void CurrentDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // unhandled exceptions thrown from UI thread
            e.Handled = true;
            Log.Logger.Fatal(e.Exception, "Unhandled exception");
            Current.Shutdown();
        }

        private void AddEventLogEntry(string msg)
        {
            using (var eventLog = new EventLog("Application"))
            {
                eventLog.Source = "Application";
                eventLog.WriteEntry(msg, EventLogEntryType.Error);
            }
        }
    }
}
