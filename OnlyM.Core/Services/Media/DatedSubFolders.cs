using System;
using System.Collections.Generic;
using System.IO;

namespace OnlyM.Core.Services.Media;

internal static class DatedSubFolders
{
    public static string? GetDatedSubFolder(string rootFolder, DateTime theDate)
    {
        if (!Directory.Exists(rootFolder))
        {
            return null;
        }

        var folderNames = GetPossibleSubFolderNames(theDate);
        foreach (var folderName in folderNames)
        {
            var path = Path.Combine(rootFolder, folderName);

            if (Directory.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static HashSet<string> GetPossibleSubFolderNames(DateTime theDate)
    {
        HashSet<string> result =
        [
            $"{theDate.Year}-{theDate.Month:D2}-{theDate.Day:D2}",
            $"{theDate.Year}-{theDate.Month}-{theDate.Day}",
            $"{theDate.Year}-{theDate.Month:D2}-{theDate.Day}",
            $"{theDate.Year}-{theDate.Month}-{theDate.Day:D2}"
        ];

        return result;
    }
}
