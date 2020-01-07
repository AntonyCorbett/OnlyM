namespace OnlyM.Models
{
    public class PdfViewStyleAndDescription
    {
        public PdfViewStyle Style { get; set; }

        public string Description { get; set; }

        public override string ToString()
        {
            return Description;
        }
    }
}
