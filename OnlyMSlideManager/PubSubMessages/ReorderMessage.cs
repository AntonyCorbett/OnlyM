using OnlyMSlideManager.Models;

namespace OnlyMSlideManager.PubSubMessages
{
    internal class ReorderMessage
    {
        public SlideItem? SourceItem { get; set; }

        public string? TargetId { get; set; }
    }
}
