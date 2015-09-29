using System.Collections.Generic;
using System.Drawing;
using Types;

namespace ImageRenderer
{
    public interface IRenderer
    {
        void RenderBitmap(List<AbsoluteBlock> piactureElements, ImageLayoutInfo layout);
        void RenderPalette(ICollection<Color> colorCollection, int width, int pixelSize);
        void SetupCanvas();
        void RenderImage(ImageView imageView);
    }
}
