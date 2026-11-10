using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using GPExtractor;

namespace Viewer
{
    /// <summary>
    /// One unit on the field: what it is, its player color, what it is doing (state and pose), where it is
    /// going, and the controls that draw it.
    /// </summary>
    public sealed class FieldUnitView
    {
        internal FieldUnitView(GameObject kind, NationColorOffset color, GameObjectState state, FieldUnit movement)
        {
            Kind = kind;
            Color = color;
            State = state;
            Movement = movement;
            Life = FullLife;
        }

        /// <summary>The life the unit starts with: its descriptor's, or 1 if it gives none.</summary>
        public int FullLife => Kind.Descriptor.Life ?? 1;

        /// <summary>The life left; the unit dies when an attack takes the last of it.</summary>
        public int Life { get; internal set; }

        /// <summary>The kind of unit (its descriptor).</summary>
        public GameObject Kind { get; }

        public NationColorOffset Color { get; internal set; }

        /// <summary>Standing or walking (or the closest the unit has).</summary>
        public GameObjectState State { get; internal set; }

        /// <summary>Animation frames shown since the state began.</summary>
        public int Pose { get; internal set; }

        public FieldUnit Movement { get; }

        public bool IsSelected { get; internal set; }

        /// <summary>Whether the state is played once (an attack, dying) instead of looping.</summary>
        public bool PlaysOnce { get; internal set; }

        /// <summary>Whether the unit was killed: it dies, lies for a while and leaves the field.</summary>
        public bool IsDead { get; internal set; }

        internal int TicksLyingDead { get; set; }

        /// <summary>The enemy the unit was sent to attack, or null.</summary>
        public FieldUnitView Target { get; internal set; }

        /// <summary>Directions of the current state, or null if it is drawn from one direction only.</summary>
        public GpDirections Directions => State.Directions > 1
            ? new GpDirections(State.Directions, 1, 0, GpDirectionsSource.Descriptor, Kind.Descriptor.FileName)
            : null;

        /// <summary>The direction (0 = south, clockwise) closest to the unit's heading.</summary>
        public int Direction => Directions?.DirectionAt(Movement.Heading) ?? 0;

        // Drawing.
        internal Image Image { get; } = new Image { Stretch = Avalonia.Media.Stretch.None, IsHitTestVisible = false };

        internal Ellipse Ring { get; set; }

        internal Ellipse Marker { get; set; }

        /// <summary>Shows the life left, above a wounded unit.</summary>
        internal Rectangle LifeBar { get; set; }

        /// <summary>The frames drawn last; kept until the frames for a new state or direction are ready.</summary>
        internal FieldFrames Shown { get; set; }

        /// <summary>The frame drawn last and where its top left corner is on the field.</summary>
        internal RenderedImage Frame { get; set; }

        internal Point Origin { get; set; }

        /// <summary>Width of the selection ring, set from the first frame drawn.</summary>
        internal double RingWidth { get; set; }

        /// <summary>Where the unit's drawn pixels are on the field, or null if nothing is drawn.</summary>
        internal Rect? DrawnBounds => Frame?.Opaque is { } opaque
            ? new Rect(Origin.X + opaque.X, Origin.Y + opaque.Y, opaque.Width, opaque.Height)
            : null;
    }

    /// <summary>An attack in the attack list, with the weapon it fires, if any.</summary>
    public sealed record AttackOption(GameObjectState State, string Weapon)
    {
        public override string ToString() => State.Code + (Weapon == null ? "" : "  ·  " + Weapon);
    }

    /// <summary>The frames of a unit's state from one direction, and where its feet (the anchor) are in them.</summary>
    internal sealed record FieldFrames(IReadOnlyList<RenderedImage> Images, PixelPoint Anchor);
}
