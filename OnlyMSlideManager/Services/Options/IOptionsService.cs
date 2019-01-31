namespace OnlyMSlideManager.Services.Options
{
    public interface IOptionsService
    {
        string AppWindowPlacement { get; set; }

        string Culture { get; set; }

        void Save();
    }
}
