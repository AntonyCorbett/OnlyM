using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using Newtonsoft.Json;
using OnlyMSlideManager.Helpers;
using Serilog;
using Serilog.Events;

namespace OnlyMSlideManager.Services.Options
{
    internal class OptionsService : IOptionsService
    {
        private readonly int _optionsVersion = 1;
        private readonly Lazy<Options> _options;

        private string? _optionsFilePath;
        private string? _originalOptionsSignature;
        
        public OptionsService()
        {
            _options = new Lazy<Options>(OptionsFactory);
        }

        public string? Culture
        {
            get => _options.Value.Culture;
            set
            {
                if (_options.Value.Culture != value)
                {
                    _options.Value.Culture = value;
                }
            }
        }

        public string? AppWindowPlacement
        {
            get => _options.Value.AppWindowPlacement;
            set
            {
                if (_options.Value.AppWindowPlacement != value)
                {
                    _options.Value.AppWindowPlacement = value;
                }
            }
        }

        public LogEventLevel LogEventLevel
        {
            get => _options.Value.LogEventLevel;
            set
            {
                if (_options.Value.LogEventLevel != value)
                {
                    _options.Value.LogEventLevel = value;
                }
            }
        }

        public void Save()
        {
            try
            {
                var newSignature = GetOptionsSignature(_options.Value);

                if (_originalOptionsSignature != newSignature)
                {
                    // changed...
                    WriteOptions(_options.Value);
                    Log.Logger.Debug("Settings changed and saved");
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Could not save settings");
            }
        }

        private static string GetOptionsSignature(Options options)
        {
            // config data is small so simple solution is best...
            return JsonConvert.SerializeObject(options);
        }

        private void WriteOptions(Options? options)
        {
            if (options != null && _optionsFilePath != null)
            {
                using var file = File.CreateText(_optionsFilePath);

                var serializer = new JsonSerializer { Formatting = Formatting.Indented };
                serializer.Serialize(file, options);
                _originalOptionsSignature = GetOptionsSignature(options);
            }
        }

        private Options OptionsFactory()
        {
            Options? result = null;

            try
            {
                _optionsFilePath = FileUtils.GetUserOptionsFilePath(_optionsVersion);
                var path = Path.GetDirectoryName(_optionsFilePath);
                if (path != null)
                {
                    FileUtils.CreateDirectory(path);
                    result = ReadOptions();
                }

                result ??= new Options();

                // store the original settings so that we can determine if they have changed
                // when we come to save them
                _originalOptionsSignature = GetOptionsSignature(result);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Could not read options file");
                result = new Options();
            }
            
            return result;
        }

        private Options? ReadOptions()
        {
            if (_optionsFilePath == null || !File.Exists(_optionsFilePath))
            {
                return WriteDefaultOptions();
            }

            using var file = File.OpenText(_optionsFilePath);

            var serializer = new JsonSerializer();
            var result = (Options?)serializer.Deserialize(file, typeof(Options));
            result?.Sanitize();

            SetCulture(result?.Culture);

            return result;
        }

        private Options WriteDefaultOptions()
        {
            var result = new Options();
            
            WriteOptions(result);

            return result;
        }

        private static void SetCulture(string? cultureString)
        {
            var culture = cultureString;

            if (string.IsNullOrEmpty(culture))
            {
                culture = CultureInfo.CurrentCulture.Name;
            }

            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
                Thread.CurrentThread.CurrentUICulture = new CultureInfo(culture);
                FrameworkElement.LanguageProperty.OverrideMetadata(
                    typeof(FrameworkElement),
                    new FrameworkPropertyMetadata(
                        XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Could not set culture");
            }
        }
    }
}
