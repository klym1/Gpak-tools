using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using Types;

namespace ImageRenderer
{
    public interface IRenderer
    {
        void RenderBitmap(Bitmap bitmap, Collection<AbsoluteMultiPictureEl> piactureElements, Collection<CounterBlock> secondPartBlocks, ImageLayoutInfo layout, List<Color> imagePaletteColors);

        void RenderCounterBlocksOnBitmap(Bitmap bitMap, Collection<AbsoluteMultiPictureEl> piactureElements, Collection<CounterBlock> secondPartBlocks, ImageLayoutInfo layout, List<Color> imagePaletteColors);

        Bitmap RenderPalette(ICollection<Color> colorCollection, int width, int pixelSize);
    }
}
