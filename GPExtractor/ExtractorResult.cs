using System.Collections.ObjectModel;
using Types;

namespace GPExtractor
{
    public class ExtractorResult
    {
        public byte[] PaletteBytes { get; set; }
        public Collection<ImageLayoutInfo> LayoutCollection { get; set; }
    }
}