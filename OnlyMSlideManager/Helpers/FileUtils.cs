using System;
using System.Globalization;
using System.IO;

namespace OnlyMSlideManager.Helpers;

internal static class FileUtils
{
    private static readonly string AppNamePathSegment = "OnlyMSlideManager";
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
#pragma warning disable CA1863
                throw new Exception(string.Format(CultureInfo.CurrentCulture, Properties.Resources.CREATE_FOLDER_ERROR, folderPath));
#pragma warning restore CA1863
            }
        }
    }

    public static string GetAppMyDocsFolder()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), AppNamePathSegment);

    public static string GetPrivateSlideshowFolder()
    {
        var folder = Path.Combine(GetAppMyDocsFolder(), "Slideshows");
        CreateDirectory(folder);
        return folder;
    }

    public static string GetLogFolder() => Path.Combine(GetAppMyDocsFolder(), "Logs");

    public static string GetUserOptionsFilePath(int optionsVersion) =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppNamePathSegment,
            optionsVersion.ToString(CultureInfo.InvariantCulture),
            OptionsFileName);

    public static string GetUsersTempFolder() => Path.GetTempPath();
}
