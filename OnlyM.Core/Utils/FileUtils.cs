namespace OnlyM.Core.Utils
{
    using System;
    using System.IO;
    using System.Text.RegularExpressions;
    using Serilog;

    /// <summary>
    /// General file / folder utilities
    /// </summary>
    public static class FileUtils
    {
        private static readonly string AppNamePathSegment = "OnlyM";
        private static readonly string OptionsFileName = "options.json";

        /// <summary>
        /// Creates directory if it doesn't exist. Throws if cannot be created
        /// </summary>
        /// <param name="folderPath">Directory to create</param>
        public static void CreateDirectory(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                if (!Directory.Exists(folderPath))
                {
                    // "Could not create folder {0}"
                    throw new Exception(string.Format(Properties.Resources.CREATE_FOLDER_ERROR, folderPath));
                }
            }
        }

        /// <summary>
        /// Gets temp staging folder for slideshows
        /// </summary>
        /// <returns>Temp folder</returns>
        public static string GetUsersTempStagingFolder()
        {
            return Path.Combine(Path.GetTempPath(), AppNamePathSegment, @"Slideshows");
        }

        /// <summary>
        /// Gets the log folder
        /// </summary>
        /// <returns>Log folder</returns>
        public static string GetLogFolder()
        {
            var folder = Path.Combine(GetOnlyMMyDocsFolder(), "Logs");
            CreateDirectory(folder);
            return folder;
        }

        /// <summary>
        /// Gets the browser cache folder
        /// </summary>
        /// <returns>Log folder</returns>
        public static string GetBrowserCacheFolder()
        {
            var folder = Path.Combine(GetOnlyMMyDocsFolder(), "BrowserCache");
            CreateDirectory(folder);
            return folder;
        }

        /// <summary>
        /// Gets the application's MyDocs folder, e.g. "...MyDocuments\OnlyM"
        /// </summary>
        /// <returns>Folder path</returns>
        public static string GetOnlyMMyDocsFolder()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), AppNamePathSegment);
        }

        /// <summary>
        /// Gets the application's Media folder, e.g. "...MyDocuments\OnlyM\Media"
        /// </summary>
        /// <returns>Folder path</returns>
        public static string GetOnlyMDefaultMediaFolder()
        {
            var folder = Path.Combine(GetOnlyMMyDocsFolder(), "Media");
            CreateDirectory(folder);
            return folder;
        }

        /// <summary>
        /// Gets the application's database folder, e.g. "...MyDocuments\OnlyM\Database"
        /// </summary>
        /// <returns>Folder path</returns>
        public static string GetOnlyMDatabaseFolder()
        {
            var folder = Path.Combine(GetOnlyMMyDocsFolder(), "Database");
            CreateDirectory(folder);
            return folder;
        }

        /// <summary>
        /// Gets the file path for storing the user options
        /// </summary>
        /// <param name="commandLineIdentifier">Optional command-line id</param>
        /// <param name="optionsVersion">The options schema version</param>
        /// <returns>User Options file path.</returns>
        public static string GetUserOptionsFilePath(string commandLineIdentifier, int optionsVersion)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppNamePathSegment,
                commandLineIdentifier ?? string.Empty,
                optionsVersion.ToString(),
                OptionsFileName);
        }

        /// <summary>
        /// Gets the OnlyM application data folder.
        /// </summary>
        /// <returns>AppData folder.</returns>
        public static string GetAppDataFolder()
        {
            // NB - user-specific folder
            // e.g. C:\Users\Antony\AppData\Roaming\OnlyM
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppNamePathSegment);
            Directory.CreateDirectory(folder);
            return folder;
        }

        public static bool SafeDeleteFile(string path)
        {
            try
            {
                File.Delete(path);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string GetBrowserLogFilePath()
        {
            var folder = Path.Combine(GetOnlyMMyDocsFolder(), "BrowserLogs");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "browser.log");
        }

        public static void DeleteBrowserCacheFolder()
        {
            var folder = GetBrowserCacheFolder();

            try
            {
                Log.Logger.Information("Purging browser cache");

                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                }
            }
            catch (IOException ex)
            {
                // it's normal for the "Visited Links" file to be in use,
                // so just test that the "Cookies" file has been removed. If so
                // we assume success.
                var cookiesFile = Path.Combine(folder, "Cookies");
                if (File.Exists(cookiesFile))
                {
                    Log.Logger.Error(ex, "Could not purge browser cache");
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Could not purge browser cache");
            }
        }

        public static string GetUsersTempFolder()
        {
            return Path.GetTempPath();
        }

        /// <summary>
        /// Strip illegal chars and reserved words from a candidate filename (should not include the directory path)
        /// </summary>
        /// <param name="filename">File name to sanitize.</param>
        /// <returns>Sanitized file name.</returns>
        /// <remarks>
        /// http://stackoverflow.com/questions/309485/c-sharp-sanitize-file-name
        /// </remarks>
        public static string CoerceValidFileName(string filename)
        {
            var invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            var invalidReStr = $@"[{invalidChars}]+";

            var reservedWords = new[]
            {
                "CON", "PRN", "AUX", "CLOCK$", "NUL", "COM0", "COM1", "COM2", "COM3", "COM4",
                "COM5", "COM6", "COM7", "COM8", "COM9", "LPT0", "LPT1", "LPT2", "LPT3", "LPT4",
                "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
            };

            var sanitisedNamePart = Regex.Replace(filename, invalidReStr, "_");
            foreach (var reservedWord in reservedWords)
            {
                var reservedWordPattern = $"^{reservedWord}\\.";
                sanitisedNamePart = Regex.Replace(sanitisedNamePart, reservedWordPattern, "_reservedWord_.", RegexOptions.IgnoreCase);
            }

            return sanitisedNamePart;
        }
    }
}
