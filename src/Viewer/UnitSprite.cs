using System.Collections.Generic;
using System.Linq;
using Avalonia;
using GPExtractor;
using ImageRenderer;

namespace Viewer
{
    /// <summary>A sprite of a unit, in either format the game uses: GP or RLC.</summary>
    internal sealed class UnitSprite
    {
        private readonly GpFile _gp;
        private readonly RlcFile _rlc;

        private UnitSprite(GpFile gp, RlcFile rlc)
        {
            _gp = gp;
            _rlc = rlc;
        }

        public int FrameCount => _gp?.Frames.Count ?? _rlc.Frames.Count;

        public IEnumerable<PixelRect> Bounds => _gp != null
            ? _gp.Frames.SelectMany(frame => frame.Parts).Select(part => new PixelRect(part.X, part.Y, part.Width, part.Height))
            : _rlc.Frames.Select(frame => new PixelRect(0, 0, frame.Width, frame.Height));

        /// <summary>The rectangle around what a frame draws, or null if it draws nothing.</summary>
        public PixelRect? BoundsOf(int frame)
        {
            if (_rlc != null)
            {
                return new PixelRect(0, 0, _rlc.Frames[frame].Width, _rlc.Frames[frame].Height);
            }

            var parts = _gp.Frames[frame].Parts;
            if (parts.Count == 0)
            {
                return null;
            }

            var left = parts.Min(part => part.X);
            var top = parts.Min(part => part.Y);
            return new PixelRect(left, top, parts.Max(part => part.X + part.Width) - left, parts.Max(part => part.Y + part.Height) - top);
        }

        public static UnitSprite Load(GameCatalog catalog, string fileName)
        {
            if (catalog.Sprite(fileName) is { } gp)
            {
                return new UnitSprite(gp, null);
            }

            return catalog.RlcSprite(fileName) is { } rlc ? new UnitSprite(null, rlc) : null;
        }

        public int EstimateAnchorX(int directions)
        {
            return _gp != null
                ? GpDirections.EstimateAnchorX(_gp, directions)
                : _rlc.Frames[0].Width / 2;
        }

        public RenderedFrame Render(
              GpRenderer renderer
            , int frame
            , int width
            , int height
            , int offsetX
            , bool mirror
            , int offsetY
        )
        {
            return _gp != null
                ? renderer.Render(
                      _gp
                    , _gp.Frames[frame]
                    , width
                    , height
                    , offsetX
                    , mirror
                    , offsetY
                )
                : renderer.Render(
                      _rlc.Frames[frame]
                    , width
                    , height
                    , offsetX
                    , mirror
                    , offsetY
                );
        }
    }
}
