using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Types;

namespace ImageRenderer
{
    public interface IRenderer
    {
        void RenderBitmap(Bitmap bitmap, Collection<MultiPictureEl> piactureElements, ImageLayoutInfo layout);
        void RenderColorStripesOnBitmap(Bitmap bitmap, Collection<MultiPictureEl> piactureElements, ImageLayoutInfo layout, ICollection<Color> colorCollection);

        Bitmap RenderPalette(ICollection<Color> colorCollection, int width, int pixelSize);

        void DrawHorizontalColorLine(Bitmap bitmap, ICollection<Color> colorCollection, int offsetX, int offsetY, int height = 1);
    }
}
