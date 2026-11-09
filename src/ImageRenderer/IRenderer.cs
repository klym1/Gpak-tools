using Types;

namespace ImageRenderer
{
    public interface IRenderer
    {
        void SetupCanvas(ImageView canvas);
        void RenderImage(ImageView canvas, ImageView imageView);
    }
}
