using System;
using System.IO;
using Fclp;

namespace OnlyM.Core.Services.CommandLine
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class CommandLineService : ICommandLineService
    {
        public CommandLineService()
        {
            var p = new FluentCommandLineParser();

            p.Setup<bool>("nogpu")
                .Callback(s => NoGpu = s).SetDefault(false);

            p.Setup<string?>("id")
                .Callback(s => OptionsIdentifier = s).SetDefault(null);

            p.Setup<bool>("nosettings")
                .Callback(s => NoSettings = s).SetDefault(false);

            p.Setup<bool>("nofolder")
                .Callback(s => NoFolder = s).SetDefault(false);

            p.Setup<string?>("source")
                .Callback(s => SourceFolder = GetFullSourcePath(s)).SetDefault(null);

            p.Setup<bool>("novidfix")
                .Callback(s => DisableVideoRenderingFix = s).SetDefault(false);

            p.Parse(Environment.GetCommandLineArgs());
        }

        public bool NoGpu { get; set; }

        public string? OptionsIdentifier { get; set; }

        public bool NoSettings { get; set; }

        public bool NoFolder { get; set; }

        public string? SourceFolder { get; set; }

        public bool DisableVideoRenderingFix { get; set; }

        private static string? GetFullSourcePath(string? sourcePath)
        {
            try
            {
                if (!string.IsNullOrEmpty(sourcePath))
                {
                    return Path.GetFullPath(sourcePath);
                }
            }
#pragma warning disable RCS1075 // Avoid empty catch clause that catches System.Exception.
#pragma warning disable CC0004 // Catch block cannot be empty
            catch (Exception)
            {
                // ignored
            }
#pragma warning restore CC0004 // Catch block cannot be empty
#pragma warning restore RCS1075 // Avoid empty catch clause that catches System.Exception.

            return null;
        }
    }
}
