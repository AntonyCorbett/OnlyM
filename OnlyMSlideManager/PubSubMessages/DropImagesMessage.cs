namespace OnlyMSlideManager.PubSubMessages
{
    using System.Collections.Generic;

    internal class DropImagesMessage
    {
        public List<string> FileList { get; set; }

        public string TargetId { get; set; }
    }
}
