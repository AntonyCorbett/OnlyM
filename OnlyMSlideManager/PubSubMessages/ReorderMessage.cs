namespace OnlyMSlideManager.PubSubMessages
{
    using OnlyMSlideManager.Models;

    internal class ReorderMessage
    {
        public SlideItem SourceItem { get; set; }

        public string TargetId { get; set; }
    }
}
