using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using GPExtractor;

namespace Viewer
{
    /// <summary>What units do to each other: attacks, fighting back, life and death.</summary>
    public partial class MainWindow
    {
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<GameObject, IReadOnlyList<GameObjectState>> AttacksByKind = new();

        // The attack each kind of unit shows first.
        private readonly Dictionary<GameObject, GameObjectState> _attackStates = new();

        /// <summary>
        /// The selected units stop and play an attack once, with its sounds, then stand again: the attack of
        /// that code (e.g. <c>ATTACK2</c>) or picked in the attack list, or else each unit's own first choice.
        /// </summary>
        public void AttackWithSelected(string code = null)
        {
            code ??= (AttackBox.SelectedItem as AttackOption)?.State.Code;

            foreach (var unit in SelectedFieldUnits)
            {
                var attack = AttacksOf(unit.Kind).FirstOrDefault(state => state.Code == code) ?? AttackStateOf(unit.Kind);
                if (attack == null)
                {
                    continue;
                }

                unit.Target = null;
                Halt(unit);
                BeginAttack(unit, attack);
            }

            UpdateFieldView();
        }

        /// <summary>
        /// The selected units die, with their death sound: they play their death once, lie dead for a while and
        /// leave the field.
        /// </summary>
        public void KillSelected()
        {
            foreach (var unit in SelectedFieldUnits)
            {
                Kill(unit);
            }

            UpdateFieldView();
        }

        /// <summary>
        /// Sends the selected units of another color against a unit: each comes within reach of its attack
        /// (the one picked in the attack list, if it has it), turns to the enemy and attacks, which kills it.
        /// </summary>
        public void AttackUnit(FieldUnitView target)
        {
            foreach (var unit in SelectedFieldUnits.Where(unit => IsEnemy(unit, target) && CanFight(unit, target)))
            {
                unit.Target = target;
                unit.TargetIsOrdered = true;
            }

            UpdateFieldView();
        }

        /// <summary>
        /// Tells the selected units that can (<c>STANDGROUND</c>) to hold their place, or to stop holding it: a
        /// unit holding its place attacks the enemies that come within reach, but goes after one only when sent.
        /// </summary>
        public void StandGroundWithSelected(bool hold)
        {
            foreach (var unit in SelectedFieldUnits.Where(unit => unit.Kind.Descriptor.CanStandGround))
            {
                unit.StandsGround = hold;
            }

            UpdateFieldView();
        }

        private static bool IsEnemy(FieldUnitView unit, FieldUnitView other) =>
            !ReferenceEquals(unit, other) && unit.Color.Offset != other.Color.Offset;

        private void Kill(FieldUnitView unit)
        {
            Halt(unit);
            unit.Next.Clear();
            unit.Target = null;
            unit.IsDead = true;
            unit.IsSelected = false;

            var death = StateOf(unit.Kind, "DEATH");
            if (death != null)
            {
                PlayOnce(unit, death);
            }
        }

        /// <summary>The attack a unit uses on an enemy: the one picked in the attack list, or its first choice.</summary>
        private GameObjectState ChosenAttackOf(GameObject kind)
        {
            var code = (AttackBox.SelectedItem as AttackOption)?.State.Code;
            return AttacksOf(kind).FirstOrDefault(state => state.Code == code) ?? AttackStateOf(kind);
        }

        /// <summary>
        /// Whether a unit is near enough to hit with an attack: one that fires a weapon reaches as far as its
        /// <c>ATTACK_RADIUS</c>, measured on the ground.
        /// </summary>
        private static bool IsWithinReach(FieldUnitView unit, FieldUnitView target, GameObjectState attack)
        {
            var from = unit.Movement.Position;
            var to = target.Movement.Position;

            // Large units cannot come as near as small ones.
            var room = RadiusOf(unit) + RadiusOf(target) + 4;
            return Fires(unit.Kind, attack)
                ? FieldPath.GroundDistance(from, to) <= Math.Max(ShotReachOf(unit, attack.Code), room)
                : Distance(from, to) <= Math.Max(MeleeReach, room);
        }

        private static bool Fires(GameObject kind, GameObjectState attack) =>
            kind.Descriptor.Weapons.Any(weapon => GameCatalog.SameAnimation(weapon.Animation, attack.Code));

        /// <summary>Whether a target is nearer than an attack that fires a weapon can be made: the minimum of its <c>ATTACK_RADIUS</c>.</summary>
        private static bool IsTooNear(FieldUnitView unit, FieldUnitView target, GameObjectState attack) =>
            Fires(unit.Kind, attack)
            && FieldPath.GroundDistance(unit.Movement.Position, target.Movement.Position) < (unit.Kind.Descriptor.AttackMinRangeOf(attack.Code) ?? 0);

        /// <summary>Whether a target is at a distance at which the unit chooses an attack: within its <c>DET_RADIUS</c>, if it has one.</summary>
        private static bool IsChosenAt(FieldUnitView unit, FieldUnitView target, GameObjectState attack)
        {
            var away = FieldPath.GroundDistance(unit.Movement.Position, target.Movement.Position);
            return unit.Kind.Descriptor.DetectionRadiusOf(attack.Code) is not { } band || (away >= band.Min && away <= band.Max);
        }

        /// <summary>
        /// The attack a unit makes on a target, of those the target is not too near for: the one it chooses at
        /// that distance, the picked one first, or else just the picked one or the first of the others. Null if
        /// the target is too near for all of them.
        /// </summary>
        private static GameObjectState AttackOn(FieldUnitView unit, FieldUnitView target, GameObjectState chosen)
        {
            var attacks = AttacksOf(unit.Kind)
                .Prepend(chosen)
                .Where(attack => unit.Kind.Descriptor.CanAttack(target.Kind.Descriptor, attack.Code) && !IsTooNear(unit, target, attack))
                .ToList();
            return attacks.FirstOrDefault(attack => IsChosenAt(unit, target, attack)) ?? attacks.FirstOrDefault();
        }

        /// <summary>Whether a unit can attack a target from where it stands.</summary>
        private bool CanReach(FieldUnitView unit, FieldUnitView target) =>
            ChosenAttackOf(unit.Kind) is { } chosen
            && AttackOn(unit, target, chosen) is { } attack
            && IsWithinReach(unit, target, attack);

        private static double ShotReachOf(FieldUnitView unit, string attack) =>
            unit.Kind.Descriptor.AttackRangeOf(attack) ?? ShotReach;

        /// <summary>
        /// An idle unit that is attacked turns on its attacker, if it can attack at all, and if it need not
        /// leave a place it holds.
        /// </summary>
        private void FightBack(FieldUnitView unit, FieldUnitView attacker)
        {
            if (CanTakeOn(unit, attacker))
            {
                TakeOn(unit, attacker);
            }
        }

        /// <summary>An idle unit takes on the nearest enemy within its <c>SEARCH_ENEMY_RADIUS</c>.</summary>
        private void LookForEnemy(FieldUnitView unit)
        {
            if (unit.Kind.Descriptor.SearchEnemyRadius is not { } radius)
            {
                return;
            }

            if (!IsIdle(unit))
            {
                return;
            }

            FieldUnitView enemy = null;
            double nearest = radius;
            foreach (var other in _fieldUnits)
            {
                if (!IsEnemy(unit, other) || other.IsDead)
                {
                    continue;
                }

                var away = FieldPath.GroundDistance(unit.Movement.Position, other.Movement.Position);
                if (away <= nearest && (enemy == null || away < nearest) && CanTakeOn(unit, other))
                {
                    enemy = other;
                    nearest = away;
                }
            }

            if (enemy != null)
            {
                TakeOn(unit, enemy);
            }
        }

        private static bool IsIdle(FieldUnitView unit) =>
            unit.Target == null && !unit.IsDead && !unit.PlaysOnce && !unit.Movement.IsWalking;

        private bool CanTakeOn(FieldUnitView unit, FieldUnitView enemy) =>
            IsIdle(unit) && CanFight(unit, enemy) && (!unit.StandsGround || CanReach(unit, enemy));

        /// <summary>Whether a unit has an attack it makes on another, made of what that is (<c>CANKILL</c>, <c>ATTMASK</c>).</summary>
        private static bool CanFight(FieldUnitView unit, FieldUnitView enemy) =>
            AttacksOf(unit.Kind).Any(attack => unit.Kind.Descriptor.CanAttack(enemy.Kind.Descriptor, attack.Code));

        private static void TakeOn(FieldUnitView unit, FieldUnitView enemy)
        {
            unit.Target = enemy;
            unit.TargetIsOrdered = false;
        }

        /// <summary>
        /// An attack lands on a unit: it loses the attack's damage (or the share of it that reaches the unit),
        /// less its armor, from its life (all of it, if the attacker has no damage given) and dies when none is left.
        /// </summary>
        private void Hit(GameObject attacker, string attack, FieldUnitView target, double share = 1)
        {
            if (!attacker.Descriptor.CanAttack(target.Kind.Descriptor, attack))
            {
                return;
            }

            var damage = attacker.Descriptor.DamageTo(target.Kind.Descriptor, attack, share) ?? target.Life;
            target.Life = Math.Max(0, target.Life - damage);
            if (target.Life == 0)
            {
                Kill(target);
            }
        }

        /// <summary>
        /// Goes after the unit's target: walks until it is within reach, then turns to it and attacks.
        /// </summary>
        private void Pursue(FieldUnitView unit, bool resting)
        {
            var target = unit.Target;
            var attack = ChosenAttackOf(unit.Kind);
            if (target.IsDead || !_fieldUnits.Contains(target) || attack == null)
            {
                unit.Target = null;
                Halt(unit);
                Unready(unit);
                return;
            }

            if (unit.PlaysOnce)
            {
                return;
            }

            var way = target.Movement.Position - unit.Movement.Position;
            attack = AttackOn(unit, target, attack);
            if (attack == null || IsWithinReach(unit, target, attack))
            {
                Halt(unit);
                unit.Movement.TurnTo(FieldUnit.HeadingOf(way));
                var turning = unit.Movement.Turn();
                if (attack == null || resting || turning)
                {
                    SetFieldState(unit, IdleStateOf(unit));
                    return;
                }

                BeginAttack(unit, attack);
                FightBack(target, unit);
            }
            else if (unit.StandsGround && !unit.TargetIsOrdered)
            {
                unit.Target = null;
                Halt(unit);
                Unready(unit);
            }
            else
            {
                // Plan the way again when the enemy has moved, or the unit was stopped on its way.
                var goal = target.Movement.Position;
                if (unit.Goal is not { } planned || Distance(planned, goal) > RetargetDistance || !unit.Movement.IsWalking)
                {
                    WalkTo(unit, goal, other => ReferenceEquals(other, target));
                }
            }
        }

        private static bool IsAttack(GameObjectState state) => state.Code.StartsWith("ATTACK", StringComparison.Ordinal);

        /// <summary>
        /// The state that goes with an attack: PATTACK1, PSTAND1 and UATTACK1 for ATTACK1. The first attack is
        /// ATTACK or ATTACK0, and its states carry no number. Null if the unit has none.
        /// </summary>
        private static GameObjectState StateFor(GameObject kind, string prefix, string attack)
        {
            var number = attack.Substring("ATTACK".Length);
            return kind.States.FirstOrDefault(state => state.Code == prefix + number)
                   ?? (number == "0" ? kind.States.FirstOrDefault(state => state.Code == prefix) : null);
        }

        /// <summary>Starts an attack: the unit first brings its weapon up, if it has that and is not ready yet.</summary>
        private void BeginAttack(FieldUnitView unit, GameObjectState attack)
        {
            unit.Next.Clear();
            var prepare = unit.ReadyFor == attack.Code ? null : StateFor(unit.Kind, "PATTACK", attack.Code);
            unit.ReadyFor = attack.Code;

            if (prepare == null)
            {
                PlayOnce(unit, attack);
                return;
            }

            unit.Next.Enqueue(attack);
            PlayOnce(unit, prepare);
        }

        /// <summary>How the unit stands between attacks: weapon ready (PSTAND) if it is, else at ease.</summary>
        private static GameObjectState IdleStateOf(FieldUnitView unit)
        {
            var ready = unit.ReadyFor == null ? null : StateFor(unit.Kind, "PSTAND", unit.ReadyFor);
            return ready ?? StandStateOf(unit.Kind);
        }

        /// <summary>The unit lowers its weapon (UATTACK), if it had it ready, and stands at ease.</summary>
        private void Unready(FieldUnitView unit)
        {
            unit.Next.Clear();
            var lower = unit.ReadyFor == null ? null : StateFor(unit.Kind, "UATTACK", unit.ReadyFor);
            unit.ReadyFor = null;

            if (lower != null)
            {
                PlayOnce(unit, lower);
            }
            else
            {
                SetFieldState(unit, StandStateOf(unit.Kind));
            }
        }

        /// <summary>The next stage of a body lying dead: DEATHLIE2 after DEATHLIE1; null after the last.</summary>
        private static GameObjectState NextCorpseOf(FieldUnitView unit)
        {
            const string corpse = "DEATHLIE";
            return unit.State.Code.StartsWith(corpse, StringComparison.Ordinal) && int.TryParse(unit.State.Code.Substring(corpse.Length), out var stage)
                ? unit.Kind.States.FirstOrDefault(state => state.Code == corpse + (stage + 1))
                : null;
        }

        /// <summary>A unit standing at ease with nothing to do takes a rest (REST) now and then, if it has one.</summary>
        private void RestWhenIdle(FieldUnitView unit)
        {
            var idle = !unit.PlaysOnce && !unit.Movement.IsWalking && unit.Target == null
                       && ReferenceEquals(unit.State, StandStateOf(unit.Kind));
            if (!idle)
            {
                unit.IdleTicks = 0;
                return;
            }

            if (unit.RestAfter == 0)
            {
                unit.RestAfter = _random.Next(MinTicksToRest, MaxTicksToRest);
            }

            if (++unit.IdleTicks < unit.RestAfter)
            {
                return;
            }

            unit.IdleTicks = 0;
            unit.RestAfter = 0;
            var rest = unit.Kind.States.FirstOrDefault(state => state.Code == "REST");
            if (rest != null)
            {
                PlayOnce(unit, rest);
            }
        }

        /// <summary>
        /// The attack to show: the first of the unit's attacks (ATTACK, ATTACK1, …) that sounds, such as a
        /// shot, or just its first attack; null if it has none.
        /// </summary>
        private GameObjectState AttackStateOf(GameObject unit)
        {
            if (!_attackStates.TryGetValue(unit, out var attack))
            {
                var attacks = AttacksOf(unit);
                attack = attacks.FirstOrDefault(state => SoundsOf(unit, state).Count > 0) ?? attacks.FirstOrDefault();
                _attackStates[unit] = attack;
            }

            return attack;
        }

        private static IReadOnlyList<GameObjectState> AttacksOf(GameObject unit)
        {
            return AttacksByKind.GetValue(
                  unit
                , kind => kind.States
                    .Where(state => state.Code.StartsWith("ATTACK", StringComparison.Ordinal) && !state.Code.Contains('+'))
                    .ToList()
            );
        }

        /// <summary>Offers the attacks of the first selected unit, starting with its first choice.</summary>
        private void UpdateAttackBox()
        {
            var kind = _fieldUnits.FirstOrDefault(unit => unit.IsSelected)?.Kind;
            if (ReferenceEquals(kind, _attackBoxKind))
            {
                return;
            }

            _attackBoxKind = kind;
            var options = kind == null
                ? new List<AttackOption>()
                : AttacksOf(kind)
                    .Select(state => new AttackOption(
                          state
                        , kind.Descriptor.Weapons.FirstOrDefault(weapon => GameCatalog.SameAnimation(weapon.Animation, state.Code))?.Weapon
                    ))
                    .ToList();

            AttackBox.ItemsSource = options;
            AttackBox.SelectedItem = kind == null ? null : options.FirstOrDefault(option => ReferenceEquals(option.State, AttackStateOf(kind)));
            AttackBox.IsVisible = options.Count > 1;
        }
    }
}
