namespace OnlyMSlideManager.Helpers
{
    using System;
    using System.IO;

    internal static class FileUtils
    {
        private static readonly string AppNamePathSegment = "OnlyMSlideManager";

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

        public static string GetAppMyDocsFolder()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), AppNamePathSegment);
        }

        public static string GetPrivateSlideshowFolder()
        {
            string folder = Path.Combine(GetAppMyDocsFolder(), @"Slideshows");
            CreateDirectory(folder);
            return folder;
        }

        public static string GetLogFolder()
        {
            return Path.Combine(
                GetAppMyDocsFolder(),
                "Logs");
        }
    }
}
