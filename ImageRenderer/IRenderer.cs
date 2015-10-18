using System.Collections.Generic;
using System.Drawing;
using Types;

namespace ImageRenderer
{
    public interface IRenderer
    {
        void RenderPalette(ICollection<Color> colorCollection, int width, int pixelSize);
        void SetupCanvas();
        void RenderImage(ImageView imageView);
    }
}
