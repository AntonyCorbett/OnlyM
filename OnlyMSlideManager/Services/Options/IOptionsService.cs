namespace OnlyMSlideManager.Services.Options
{
    public interface IOptionsService
    {
        string AppWindowPlacement { get; set; }

        void Save();
    }
}
