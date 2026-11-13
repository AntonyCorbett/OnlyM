using Newtonsoft.Json;
using OnlyM.Core.Services.Options;
using OnlyM.Core.Utils;

namespace OnlyM.Core.Tests;

public class OptionsRecentFoldersTests
{
    [Fact]
    public void LoadedOptions_DoNotDuplicateTheDefaultMediaFolder()
    {
        // The Options constructor seeds the recent list with the default folder and Json.NET appends
        // the saved folders to that list - mirror OptionsService.ReadOptions.
        var defaultFolder = FileUtils.GetOnlyMDefaultMediaFolder(); // creates the folder if necessary

        var json = JsonConvert.SerializeObject(new { RecentlyUsedMediaFolders = new[] { defaultFolder } });

        using var reader = new StringReader(json);
        var options = (Options?)new JsonSerializer().Deserialize(reader, typeof(Options));
        options!.Sanitize();

        Assert.Equal([defaultFolder], options.RecentlyUsedMediaFolders);
    }

    [Fact]
    public void Sanitize_RemovesDuplicateRecentFolders_IgnoringCase()
    {
        var folder = Directory.CreateTempSubdirectory("OnlyMRecent").FullName;
        try
        {
            var options = new Options();
            options.RecentlyUsedMediaFolders.Clear();
            options.RecentlyUsedMediaFolders.AddRange([folder, folder.ToUpperInvariant(), folder]);

            options.Sanitize();

            Assert.Equal([folder], options.RecentlyUsedMediaFolders);
        }
        finally
        {
            Directory.Delete(folder);
        }
    }
}
