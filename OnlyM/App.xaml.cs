using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using CefSharp;
using CefSharp.Wpf;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using OnlyM.AutoUpdates;
using OnlyM.Core.Services.CommandLine;
using OnlyM.Core.Services.Database;
using OnlyM.Core.Services.Media;
using OnlyM.Core.Services.Monitors;
using OnlyM.Core.Services.Options;
using OnlyM.Core.Utils;
using OnlyM.CoreSys.Services.Snackbar;
using OnlyM.Services.Dialogs;
using OnlyM.Services.DragAndDrop;
using OnlyM.Services.FrozenVideoItems;
using OnlyM.Services.HiddenMediaItems;
using OnlyM.Services.MediaChanging;
using OnlyM.Services.Pages;
using OnlyM.Services.PdfOptions;
using OnlyM.Services.StartOffsetStorage;
using OnlyM.Services.WebBrowser;
using OnlyM.ViewModel;
using Serilog;

namespace OnlyM
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "Singleton")]
    public partial class App : Application
    {
        private readonly string _appString = "OnlyMMeetingMedia";
        private readonly bool _successCefSharp;
        private Mutex? _appMutex;
        
        public App()
        {
            try
            {
                Unosquare.FFME.Library.FFmpegDirectory = FMpegFolderName;

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
                ConfigureServices();

                Current.DispatcherUnhandledException += CurrentDispatcherUnhandledException;
            }

            if (!_successCefSharp)
            {
                Log.Logger.Error("Could not initialise CefSharp");
            }

            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;
        }

        private static void ConfigureServices()
        {
            var serviceCollection = new ServiceCollection();

            // services.
            serviceCollection.AddSingleton<IPageService, PageService>();
            serviceCollection.AddSingleton<IMediaProviderService, MediaProviderService>();
            serviceCollection.AddSingleton<IThumbnailService, ThumbnailService>();
            serviceCollection.AddSingleton<IMonitorsService, MonitorsService>();
            serviceCollection.AddSingleton<IOptionsService, OptionsService>();
            serviceCollection.AddSingleton<ILogLevelSwitchService, LogLevelSwitchService>();
            serviceCollection.AddSingleton<IFolderWatcherService, FolderWatcherService>();
            serviceCollection.AddSingleton<IDatabaseService, DatabaseService>();
            serviceCollection.AddSingleton<IMediaMetaDataService, MediaMetaDataService>();
            serviceCollection.AddSingleton<IDragAndDropService, DragAndDropService>();
            serviceCollection.AddSingleton<ISnackbarService, SnackbarService>();
            serviceCollection.AddSingleton<IMediaStatusChangingService, MediaStatusChangingService>();
            serviceCollection.AddSingleton<IHiddenMediaItemsService, HiddenMediaItemsService>();
            serviceCollection.AddSingleton<IFrozenVideosService, FrozenVideosService>();
            serviceCollection.AddSingleton<IPdfOptionsService, PdfOptionsService>();
            serviceCollection.AddSingleton<ICommandLineService, CommandLineService>();
            serviceCollection.AddSingleton<IActiveMediaItemsService, ActiveMediaItemsService>();
            serviceCollection.AddSingleton<IDialogService, DialogService>();
            serviceCollection.AddSingleton<IStartOffsetStorageService, StartOffsetStorageService>();

            // view models.
            serviceCollection.AddSingleton<StartOffsetViewModel>();

            serviceCollection.AddSingleton<MainViewModel>();
            serviceCollection.AddSingleton<MediaViewModel>();
            serviceCollection.AddSingleton<OperatorViewModel>();
            serviceCollection.AddSingleton<SettingsViewModel>();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            Ioc.Default.ConfigureServices(serviceProvider);
        }

        private static void ConfigureLogger()
        {
            var logsDirectory = FileUtils.GetLogFolder();

            Log.Logger = new LoggerConfiguration()
               .MinimumLevel.ControlledBy(LogLevelSwitchService.LevelSwitch)
               .WriteTo.File(Path.Combine(logsDirectory, "log-.txt"), retainedFileCountLimit: 28, rollingInterval: RollingInterval.Day)
               .CreateLogger();

            Log.Logger.Information("==== Launched ====");
            Log.Logger.Information($"Version {VersionDetection.GetCurrentVersion()}");
        }

        private bool AnotherInstanceRunning()
        {
            _appMutex = new Mutex(true, _appString, out var newInstance);
            return !newInstance;
        }

        private static bool InitCef()
        {
            //// refer here:
            //// https://github.com/cefsharp/CefSharp/blob/cefsharp/43/CefSharp.Example/CefExample.cs#L54

            var settings = new CefSettings
            {
                CachePath = FileUtils.GetBrowserCacheFolder(),
                LogFile = FileUtils.GetBrowserLogFilePath(),
                LogSeverity = LogSeverity.Error,
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
                IsCSPBypassing = true,
            });

            // prevents orphaned CelSharp.BrowserSubprocess.exe instances
            // caused by an OnlyM crash.
            CefSharpSettings.SubprocessExitIfParentProcessClosed = true;

            return Cef.Initialize(settings);
        }

        private void CurrentDispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // unhandled exceptions thrown from UI thread
            e.Handled = true;
            Log.Logger.Fatal(e.Exception, "Unhandled exception");
            Current.Shutdown();
        }

        private static void AddEventLogEntry(string msg)
        {
            using (var eventLog = new EventLog("Application"))
            {
                eventLog.Source = "Application";
                eventLog.WriteEntry(msg, EventLogEntryType.Error);
            }
        }
    }
}
