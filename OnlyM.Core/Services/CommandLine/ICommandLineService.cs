namespace OnlyM.Core.Services.CommandLine
{
    public interface ICommandLineService
    {
        bool NoGpu { get; set; }

        string OptionsIdentifier { get; set; }

        bool NoSettings { get; set; }

        bool NoFolder { get; set; }

        string SourceFolder { get; set; }

        bool DisableVideoRenderingFix { get; set; }
    }
}
