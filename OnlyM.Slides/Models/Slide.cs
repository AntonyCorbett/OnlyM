using System.Text;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;

namespace OnlyM.Slides.Models
{
    public class Slide
    {
        public string ArchiveEntryName { get; set; }

        public string OriginalFilePath { get; set; }

        [JsonIgnore]
        public BitmapImage Image { get; set; }

        public bool FadeInForward { get; set; }

        public bool FadeInReverse { get; set; }

        public bool FadeOutForward { get; set; }

        public bool FadeOutReverse { get; set; }

        public int DwellTimeMilliseconds { get; set; }

        public string CreateSignature()
        {
            var sb = new StringBuilder();

            sb.Append(FadeInForward);
            sb.Append('|');
            sb.Append(FadeInReverse);
            sb.Append('|');
            sb.Append(FadeOutForward);
            sb.Append('|');
            sb.Append(FadeOutReverse);
            sb.Append('|');
            sb.Append(DwellTimeMilliseconds);
            sb.Append('|');
            sb.Append(ArchiveEntryName);
            sb.Append('|');
            sb.Append(OriginalFilePath);
            
            return sb.ToString();
        }
    }
}
