﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using Types;

namespace ImageRenderer
{
    public interface IRenderer
    {
        void RenderBitmap(Bitmap bitmap, List<AbsoluteBlock> piactureElements, ImageLayoutInfo layout);
        Bitmap RenderPalette(ICollection<Color> colorCollection, int width, int pixelSize);
        void SetupCanvas(Bitmap bitMap);
    }
}
