namespace OnlyM.Core.Services.CommandLine
{
    using System;
    using Fclp;

    // ReSharper disable once ClassNeverInstantiated.Global
    public class CommandLineService : ICommandLineService
    {
        public CommandLineService()
        {
            var p = new FluentCommandLineParser();

            p.Setup<bool>("nogpu")
                .Callback(s => { NoGpu = s; }).SetDefault(false);

            p.Setup<string>("id")
                .Callback(s => { OptionsIdentifier = s; }).SetDefault(null);

            p.Setup<bool>("nosettings")
                .Callback(s => { NoSettings = s; }).SetDefault(false);

            p.Setup<bool>("nofolder")
                .Callback(s => { NoFolder = s; }).SetDefault(false);

            p.Parse(Environment.GetCommandLineArgs());
        }

        public bool NoGpu { get; set; }

        public string OptionsIdentifier { get; set; }

        public bool NoSettings { get; set; }

        public bool NoFolder { get; set; }
    }
}
