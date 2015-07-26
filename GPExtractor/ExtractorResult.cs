using System.Collections.ObjectModel;

namespace GPExtractor
{
    public class ExtractorResult
    {
        public byte[] PaletteBytes { get; set; }
        public byte[] ImageBytes { get; set; }
        public Collection<ImageLayoutInfo> LayoutCollection { get; set; }
    }
}