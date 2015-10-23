using System.Collections.Generic;
using System.Drawing;
using Types;

namespace ImageRenderer
{
    public interface IRenderer
    {
        void SetupCanvas(Bitmap bitmap);
        void RenderImage(Bitmap bitmap, ImageView imageView);
    }
}
