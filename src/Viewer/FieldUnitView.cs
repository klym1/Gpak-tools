using System.Collections.Generic;
using Avalonia;
using GPExtractor;

namespace Viewer
{
    /// <summary>
    /// One unit on the field: what it is, its player color, what it is doing (state and pose), where it is
    /// going, and how it is drawn.
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

        /// <summary>Ticks the unit still rests after its last attack before it can attack again.</summary>
        public int PauseTicksLeft { get; internal set; }

        /// <summary>How long the rest after its last attack is in all.</summary>
        internal int PauseTicks { get; set; }

        /// <summary>Whether the unit holds its place: it attacks what comes within reach but does not go after it.</summary>
        public bool StandsGround { get; internal set; }

        /// <summary>Whether the unit was sent against its target, and did not pick it itself.</summary>
        internal bool TargetIsOrdered { get; set; }

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

        /// <summary>Where the unit is going, by way of <see cref="Route"/>; null when it has nowhere to go.</summary>
        public Point? Goal { get; internal set; }

        /// <summary>The points still to walk to after the one it is walking to now.</summary>
        internal Queue<Point> Route { get; set; } = new Queue<Point>();

        /// <summary>Ticks spent waiting for a unit in the way, and ways round tried since the last step.</summary>
        internal int WaitTicks { get; set; }

        internal int Detours { get; set; }

        /// <summary>For smoke: the color it is laid over the field in; null for everything drawn as it is.</summary>
        internal System.Drawing.Color? Fog { get; set; }

        /// <summary>For a projectile: where it flies from and to on the ground, how high and for how long.</summary>
        internal (Point From, Point To, double Height, int Ticks)? Flight { get; set; }

        internal int FlightAge { get; set; }

        /// <summary>For a projectile: where its shadow is on the ground.</summary>
        internal Point? Shadow { get; set; }

        /// <summary>How long the unit's last shot takes to land, in ticks: its impact is heard then.</summary>
        internal int ShotTicks { get; set; }

        /// <summary>States to play once, in turn, after the one playing now (an attack after its preparation).</summary>
        internal Queue<GameObjectState> Next { get; } = new Queue<GameObjectState>();

        /// <summary>The attack the unit has its weapon ready for, or null when it stands at ease.</summary>
        internal string ReadyFor { get; set; }

        /// <summary>Ticks the unit has stood idle, and after how many it takes a rest.</summary>
        internal int IdleTicks { get; set; }

        internal int RestAfter { get; set; }

        /// <summary>The way the unit turns once it is at its place in a formation (as its heading), or null.</summary>
        internal double? FacingAtGoal { get; set; }

        /// <summary>The enemy the unit was sent to attack, or null.</summary>
        public FieldUnitView Target { get; internal set; }

        /// <summary>Directions of the current state, or null if it is drawn from one direction only.</summary>
        public GpDirections Directions => State.Directions > 1
            ? new GpDirections(State.Directions, 1, 0, GpDirectionsSource.Descriptor, Kind.Descriptor.FileName)
            : null;

        /// <summary>The direction (0 = south, clockwise) closest to the unit's heading.</summary>
        public int Direction => Directions?.DirectionAt(Movement.Heading) ?? 0;

        // Drawing.
        /// <summary>Whether the unit is drawn: not when it is hidden by the fog of war.</summary>
        internal bool IsShown { get; set; } = true;

        /// <summary>Shows the life left, above a wounded unit.</summary>
        internal FieldBar LifeBar { get; set; }

        /// <summary>Shows how far the rest after an attack has got, under the life bar.</summary>
        internal FieldBar ReloadBar { get; set; }

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

    /// <summary>A bar over a unit: whether it is shown, and how wide its filled part is.</summary>
    internal readonly record struct FieldBar(bool IsVisible, double Width);

    /// <summary>An attack in the attack list, with the weapon it fires, if any.</summary>
    public sealed record AttackOption(GameObjectState State, string Weapon)
    {
        public override string ToString() => State.Code + (Weapon == null ? "" : "  ·  " + Weapon);
    }

    /// <summary>The frames of a unit's state from one direction, and where its feet (the anchor) are in them.</summary>
    internal sealed record FieldFrames(IReadOnlyList<RenderedImage> Images, PixelPoint Anchor);
}
