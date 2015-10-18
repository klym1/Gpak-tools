using System.Collections.Generic;
using System.Drawing;
using Types;

namespace ImageRenderer
{
    public interface IRenderer
    {
        void RenderPalette(Bitmap bitmap, ICollection<Color> colorCollection, int width, int pixelSize);
        void SetupCanvas(Bitmap bitmap);
        void RenderImage(Bitmap bitmap, ImageView imageView);
    }
}
