using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using NUnit.Framework;
using GPExtractor;
using Viewer;

[assembly: AvaloniaTestApplication(typeof(ViewerTests.TestAppBuilder))]

namespace ViewerTests
{
    public class TestAppBuilder
    {
        // Headless platform with real Skia rendering, so frames can be captured.
        public static AppBuilder BuildAvaloniaApp()
        {
            // Keep the tests away from the user's remembered game data.
            ViewerSettings.File = Path.Combine(Path.GetTempPath(), "viewer-tests-" + Guid.NewGuid(), "last-game-data.txt");

            return AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont();
        }
    }

    public class MainWindowTests
    {
        [AvaloniaTest]
        public void StartsEmptyAndAsksForGameData()
        {
            var window = new MainWindow();
            window.Show();

            Assert.That(window.FindControl<StackPanel>("UnitPanel").IsVisible, Is.False);
            Assert.That(window.FindControl<TextBlock>("FieldHint").Text, Does.StartWith("Open the game's All.gsc"));
            Assert.That(window.FieldUnits, Is.Empty);
            Assert.That(window.FindControl<ToggleButton>("FieldButton"), Is.Null, "the field is the only mode");
            Assert.That(window.FindControl<ComboBox>("StateBox"), Is.Null);
            Assert.That(window.FindControl<ScrollViewer>("ImageScroller"), Is.Null);

            window.Close();
        }

        [AvaloniaTest]
        public async Task ReportsMissingFile()
        {
            var missing = Path.Combine(Path.GetTempPath(), "does-not-exist.gsc");
            var window = new MainWindow(missing);
            window.Show();
            await window.RenderingCompleted;

            Assert.That(window.FindControl<TextBlock>("StatusText").Text, Does.StartWith("Failed to open"));
            Assert.That(ViewerSettings.LastGameData, Is.Not.EqualTo(missing), "only game data that opened is remembered");
            Assert.That(window.FieldUnits, Is.Empty);

            window.Close();
        }

        [AvaloniaTest]
        public async Task ReportsASpriteWithoutGameData()
        {
            var folder = Path.Combine(Path.GetTempPath(), "viewer-lone-sprite-" + Guid.NewGuid());
            // Nested, so no stray descriptors of the temp folder are picked up.
            var spriteFolder = Path.Combine(folder, "a", "b");
            Directory.CreateDirectory(spriteFolder);
            var sprite = Path.Combine(spriteFolder, "LONE.GP");
            File.WriteAllBytes(sprite, UnitTests.GpTestFiles.Unit(poses: 1, anchorX: 10));

            try
            {
                var window = new MainWindow(sprite);
                window.Show();
                await window.RenderingCompleted;

                Assert.That(window.FindControl<TextBlock>("StatusText").Text, Does.StartWith("No game data"));
                Assert.That(window.Catalog, Is.Null);

                window.Close();
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [AvaloniaTest]
        public async Task OpensTheGameDataAroundASprite()
        {
            var folder = Path.Combine(Path.GetTempPath(), "viewer-sprite-units-" + Guid.NewGuid());
            Directory.CreateDirectory(folder);
            foreach (var (name, content) in UnitTests.GpTestFiles.UnitGameData())
            {
                File.WriteAllBytes(Path.Combine(folder, name), content);
            }

            try
            {
                var window = new MainWindow(Path.Combine(folder, "TSTG.GP"));
                window.Show();
                await window.RenderingCompleted;

                Assert.That(window.FindControl<StackPanel>("UnitPanel").IsVisible, Is.True);
                Assert.That(window.FindControl<ComboBox>("UnitBox").ItemCount, Is.EqualTo(1), "only units, not buildings");
                Assert.That(window.FindControl<Button>("AddUnitButton"), Is.Null);
                Assert.That(window.FieldUnits.Select(unit => unit.Kind.Descriptor.FileName), Is.EqualTo(new[] { "TST", "TST" }));
                Assert.That(
                      window.FieldUnits.Select(unit => unit.Color.Offset)
                    , Is.EqualTo(new[] { NationColorOffset.Blue.Offset, NationColorOffset.Red.Offset })
                    , "each kind starts in two colors"
                );
                Assert.That(window.SelectedFieldUnits, Is.EqualTo(new[] { window.FieldUnits[0] }));
                Assert.That(window.IsPlaying, Is.True);

                window.Close();
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [AvaloniaTest]
        public async Task DrawsUnitsWhoseSpritesAreRlcFiles()
        {
            var folder = Path.Combine(Path.GetTempPath(), "viewer-rlc-" + Guid.NewGuid());
            Directory.CreateDirectory(folder);
            foreach (var (name, content) in UnitTests.GpTestFiles.UnitGameData().Where(file => file.Name != "TSTS.GP"))
            {
                File.WriteAllBytes(Path.Combine(folder, name), content);
            }

            // The stand sprite as an RLC file: one pixel at its anchor (20, 10).
            File.WriteAllBytes(Path.Combine(folder, "TSTS.RLC"), UnitTests.GpTestFiles.RlcOfDots(frames: 9, x: 20, y: 10, color: 5));

            try
            {
                var window = new MainWindow(folder);
                window.Show();
                await window.RenderingCompleted;
                window.Pause();
                window.Tick();
                await window.RenderingCompleted;

                var unit = OnlyUnit(window);
                Assert.That(unit.State.Code, Is.EqualTo("STAND"));
                Assert.That(window.FindControl<TextBlock>("StatusText").Text, Is.Empty);
                Assert.That(DrawnColumnsAtFeet(unit), Is.EqualTo(new[] { 0 }));

                window.Close();
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [AvaloniaTest]
        public async Task PlaysTheSoundsOfWalkingUnits()
        {
            var folder = UnitArchiveFolder(out var archive);
            Directory.CreateDirectory(Path.Combine(folder, "Sound"));
            File.WriteAllBytes(Path.Combine(folder, "Sound", "Step1.wav"), new byte[4]);
            File.WriteAllBytes(Path.Combine(folder, "Sound", "Step2.wav"), new byte[4]);

            try
            {
                var player = new RecordingSoundPlayer();
                var window = new MainWindow(archive) { SoundPlayer = player };
                window.Show();
                await window.RenderingCompleted;
                window.Pause();
                window.UpdateLayout();

                // Standing is silent.
                var unit = OnlyUnit(window);
                for (var i = 0; i < 8; i++)
                {
                    window.Tick();
                }

                Assert.That(player.Played, Is.Empty);

                // Walking plays the step sound from the game folder, next to the archive.
                window.SendSelectedTo(unit.Movement.Position + new Vector(0, -200));
                window.Tick();
                Assert.That(player.Played, Has.Count.EqualTo(1), "the walk has a sound at its frame 1");
                Assert.That(Path.GetDirectoryName(player.Played[0]), Is.EqualTo(Path.Combine(folder, "Sound")));
                Assert.That(Path.GetFileName(player.Played[0]), Does.Match("^Step[12]\\.wav$"));

                // Without the wave file, the status bar says where the sounds are expected.
                File.Delete(Path.Combine(folder, "Sound", "Step1.wav"));
                File.Delete(Path.Combine(folder, "Sound", "Step2.wav"));
                for (var i = 0; i < 4; i++)
                {
                    window.Tick();
                }

                Assert.That(player.Played, Has.Count.EqualTo(1));
                Assert.That(window.FindControl<TextBlock>("StatusText").Text, Does.StartWith("No sound: Sound"));

                // Not with the sound switched off.
                window.FindControl<CheckBox>("SoundBox").IsChecked = false;
                for (var i = 0; i < 8; i++)
                {
                    window.Tick();
                }

                Assert.That(player.Played, Has.Count.EqualTo(1));

                window.Close();
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [AvaloniaTest]
        public async Task AttacksAndDiesWithSounds()
        {
            var folder = UnitArchiveFolder(out var archive);
            Directory.CreateDirectory(Path.Combine(folder, "Sound"));
            File.WriteAllBytes(Path.Combine(folder, "Sound", "Alarm.wav"), new byte[4]);
            File.WriteAllBytes(Path.Combine(folder, "Sound", "Step1.wav"), new byte[4]);
            File.WriteAllBytes(Path.Combine(folder, "Sound", "Step2.wav"), new byte[4]);

            try
            {
                var player = new RecordingSoundPlayer();
                var window = new MainWindow(archive) { SoundPlayer = player };
                window.Show();
                await window.RenderingCompleted;
                window.Pause();
                window.UpdateLayout();

                var attackButton = window.FindControl<Button>("AttackButton");
                var unit = OnlyUnit(window);
                Assert.That(attackButton.IsEnabled, Is.True, "a unit is selected");

                // The attack plays once (3 frames: its sound at frame 1, its weapon's shot at frame 2), then the
                // unit stands again.
                window.SendSelectedTo(unit.Movement.Position + new Vector(-100, 0));
                window.AttackWithSelected();
                Assert.That(unit.State.Code, Is.EqualTo("ATTACK"));
                Assert.That(unit.Movement.IsWalking, Is.False, "it stops to attack");
                Assert.That(unit.Direction, Is.EqualTo(4), "facing the way it was going");
                window.Tick();
                Assert.That(player.Played.Select(Path.GetFileName), Is.EqualTo(new[] { "Alarm.wav" }));
                window.Tick();
                Assert.That(unit.State.Code, Is.EqualTo("ATTACK"));
                Assert.That(player.Played, Has.Count.EqualTo(2));
                Assert.That(Path.GetFileName(player.Played[1]), Does.StartWith("Step"), "the shot");
                window.Tick();
                Assert.That(unit.State.Code, Is.EqualTo("STAND"));
                window.Tick();
                Assert.That(player.Played, Has.Count.EqualTo(2));

                // The unit has a second attack, which can be picked in the list: a mortar, heard at once.
                var attackBox = window.FindControl<ComboBox>("AttackBox");
                Assert.That(attackBox.IsVisible, Is.True);
                Assert.That(attackBox.Items.Select(item => item.ToString()), Is.EqualTo(new[] { "ATTACK  \u00b7  CANNON", "ATTACK2  \u00b7  MORTAR" }));
                Assert.That(attackBox.SelectedIndex, Is.EqualTo(0));
                attackBox.SelectedIndex = 1;
                window.FindControl<Button>("AttackButton").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Assert.That(unit.State.Code, Is.EqualTo("ATTACK2"));
                Assert.That(Path.GetFileName(player.Played.Last()), Is.EqualTo("Alarm.wav"));
                player.Played.Clear();
                window.Tick();
                window.Tick();
                Assert.That(unit.State.Code, Is.EqualTo("STAND"));
                player.Played.AddRange(new[] { "", "" });

                // Killed: it dies with its sound (at frame 2), lies dead and cannot be selected or sent.
                window.KillSelected();
                Assert.That(unit.State.Code, Is.EqualTo("DEATH"));
                Assert.That(window.SelectedFieldUnits, Is.Empty);
                window.Tick();
                window.Tick();
                Assert.That(player.Played, Has.Count.EqualTo(3));
                window.Tick();
                Assert.That(unit.State.Code, Is.EqualTo("DEATHLIE1"));
                Click(window, unit.Movement.Position, MouseButton.Left);
                Assert.That(window.SelectedFieldUnits, Is.Empty);
                Assert.That(attackButton.IsEnabled, Is.False);

                // After a while the body is gone.
                for (var i = 0; i < 120; i++)
                {
                    window.Tick();
                }

                Assert.That(window.FieldUnits, Is.Empty);
                Assert.That(Path.GetFileName(player.Played.Last()), Does.StartWith("Step"), "the mortar shell landed a while after it was fired");
                Assert.That(player.Played, Has.Count.EqualTo(5), "with the mortar shell");

                window.Close();
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [AvaloniaTest]
        public async Task AttacksAnEnemyOnRightClick()
        {
            var folder = UnitArchiveFolder(out var archive);
            try
            {
                var window = new MainWindow(archive) { SoundPlayer = new RecordingSoundPlayer() };
                window.Show();
                await window.RenderingCompleted;
                window.Pause();
                window.UpdateLayout();

                // A blue unit, a blue friend, and a red enemy far to the east.
                var unit = OnlyUnit(window);
                unit.Movement.PlaceAt(new Point(100, 200));
                var friend = window.AddToField("TST", new Point(100, 300));
                Click(window, new Point(300, 50), MouseButton.Left); // deselect, so the friend stays blue
                await window.SetPlayerColor(NationColorOffset.Red);
                var enemy = window.AddToField("TST", new Point(600, 200));
                window.Tick();
                await window.RenderingCompleted;
                window.Tick();

                // Right-clicking a friend just goes there.
                Click(window, unit.Movement.Position, MouseButton.Left);
                Click(window, friend.Movement.Position, MouseButton.Right);
                Assert.That(unit.Target, Is.Null);
                Assert.That(unit.Movement.IsWalking, Is.True);
                unit.Movement.PlaceAt(new Point(100, 200));

                // Right-clicking the enemy: the unit walks until its cannon reaches (300 pixels), ...
                Click(window, enemy.Movement.Position, MouseButton.Right);
                Assert.That(unit.Target, Is.SameAs(enemy));
                window.Tick();
                Assert.That(unit.State.Code, Is.EqualTo("MOTION_L+MOTION_R"));
                Assert.That(unit.Direction, Is.EqualTo(12), "east");
                for (var i = 0; i < 100 && unit.State.Code != "ATTACK"; i++)
                {
                    window.Tick();
                }

                // ... stops and fires, facing it, ...
                Assert.That(unit.State.Code, Is.EqualTo("ATTACK"));
                Assert.That(unit.Movement.IsWalking, Is.False);
                Assert.That(enemy.Movement.Position.X - unit.Movement.Position.X, Is.InRange(296.0, 300.0));
                Assert.That(unit.Direction, Is.EqualTo(12));
                Assert.That(enemy.IsDead, Is.False);

                // The enemy fights back at once, ...
                Assert.That(enemy.Target, Is.SameAs(unit));
                window.Tick();
                Assert.That(enemy.State.Code, Is.EqualTo("ATTACK"));
                Assert.That(enemy.Direction, Is.EqualTo(4), "west, facing the unit");

                // After its attack (the shell left at its frame 2) the unit rests for its pause (2 ticks), then
                // attacks again.
                window.Tick();
                window.Tick();
                Assert.That(unit.State.Code, Is.EqualTo("STAND"));
                window.Tick();
                Assert.That(unit.State.Code, Is.EqualTo("STAND"));
                window.Tick();
                Assert.That(unit.State.Code, Is.EqualTo("ATTACK"));

                // Nobody is hurt while the shells fly: 20 ticks from the shot.
                for (var i = 0; i < 15; i++)
                {
                    window.Tick();
                    Assert.That((unit.Life, enemy.Life), Is.EqualTo((50, 50)));
                }

                // Then both shells land, each taking 30 less 7 of armor, of 50.
                window.Tick();
                Assert.That((unit.Life, enemy.Life), Is.EqualTo((27, 27)));
                Assert.That(enemy.LifeBar.IsVisible, Is.True);

                // They go on until the third shell kills.
                for (var i = 0; i < 100 && !enemy.IsDead; i++)
                {
                    window.Tick();
                }

                Assert.That(enemy.IsDead, Is.True);
                Assert.That(enemy.State.Code, Is.EqualTo("DEATH"));
                Assert.That(unit.Life, Is.LessThan(27));
                window.Tick();
                Assert.That(unit.Target, Is.Null);
                Assert.That(friend.State.Code, Is.EqualTo("STAND"), "units that were not selected stay out of it");

                window.Close();
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [AvaloniaTest]
        public async Task WalksAroundUnitsInsteadOfThroughThem()
        {
            var folder = UnitArchiveFolder(out var archive);
            try
            {
                var window = new MainWindow(archive) { SoundPlayer = new RecordingSoundPlayer() };
                window.Show();
                await window.RenderingCompleted;
                window.Pause();
                window.UpdateLayout();

                // A unit stands right on the walker's way east.
                var walker = OnlyUnit(window);
                walker.Movement.PlaceAt(new Point(100, 200));
                var standing = window.AddToField("TST", new Point(300, 200));
                window.Tick();
                await window.RenderingCompleted;
                window.Tick();

                // The two take 20 pixels between them; routes may cut a cell (8) into that.
                const double room = 20 - FieldPath.CellSize;

                Click(window, walker.Movement.Position, MouseButton.Left);
                Click(window, new Point(500, 200), MouseButton.Right);
                Assert.That(walker.Goal, Is.EqualTo(new Point(500, 200)));

                var nearest = double.MaxValue;
                var furthestAside = 0.0;
                for (var i = 0; i < 400 && walker.Movement.IsWalking; i++)
                {
                    window.Tick();
                    nearest = Math.Min(nearest, FieldPath.GroundDistance(walker.Movement.Position, standing.Movement.Position));
                    furthestAside = Math.Max(furthestAside, Math.Abs(walker.Movement.Position.Y - 200));
                }

                Assert.That(walker.Movement.Position, Is.EqualTo(new Point(500, 200)), "it arrives");
                Assert.That(walker.State.Code, Is.EqualTo("STAND"));
                Assert.That(nearest, Is.GreaterThanOrEqualTo(room), "it never walks through the unit");
                Assert.That(furthestAside, Is.InRange(4.0, 40.0), "it steps aside, but not far");
                Assert.That(standing.Movement.Position, Is.EqualTo(new Point(300, 200)));

                // Two units sent to each other's places pass each other and both arrive.
                standing.Movement.PlaceAt(new Point(100, 200));
                Click(window, walker.Movement.Position, MouseButton.Left);
                Click(window, new Point(100, 300), MouseButton.Right);
                for (var i = 0; i < 400 && walker.Movement.IsWalking; i++)
                {
                    window.Tick();
                }

                Click(window, walker.Movement.Position, MouseButton.Left);
                Click(window, new Point(500, 300), MouseButton.Right);
                Click(window, standing.Movement.Position, MouseButton.Left);
                Click(window, new Point(100, 300), MouseButton.Right);
                // They now walk the same line towards each other: (100, 300) to (500, 300) and (100, 200) to (100, 300).
                nearest = double.MaxValue;
                for (var i = 0; i < 600 && (walker.Movement.IsWalking || standing.Movement.IsWalking); i++)
                {
                    window.Tick();
                    nearest = Math.Min(nearest, FieldPath.GroundDistance(walker.Movement.Position, standing.Movement.Position));
                }

                Assert.That(walker.Movement.Position, Is.EqualTo(new Point(500, 300)));
                Assert.That(nearest, Is.GreaterThanOrEqualTo(room - FieldUnit.DefaultStepLength));

                window.Close();
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [AvaloniaTest]
        public async Task UnitsMeetingHeadOnPassEachOther()
        {
            var folder = UnitArchiveFolder(out var archive);
            try
            {
                var window = new MainWindow(archive) { SoundPlayer = new RecordingSoundPlayer() };
                window.Show();
                await window.RenderingCompleted;
                window.Pause();
                window.UpdateLayout();

                var east = OnlyUnit(window);
                east.Movement.PlaceAt(new Point(100, 200));
                var west = window.AddToField("TST", new Point(500, 200));
                window.Tick();
                await window.RenderingCompleted;
                window.Tick();

                // Each is sent to where the other stands.
                Click(window, east.Movement.Position, MouseButton.Left);
                Click(window, new Point(500, 200), MouseButton.Right);
                Click(window, west.Movement.Position, MouseButton.Left);
                Click(window, new Point(100, 200), MouseButton.Right);

                var nearest = double.MaxValue;
                for (var i = 0; i < 800 && (east.Movement.IsWalking || west.Movement.IsWalking); i++)
                {
                    window.Tick();
                    nearest = Math.Min(nearest, FieldPath.GroundDistance(east.Movement.Position, west.Movement.Position));
                }

                Assert.That(east.Movement.Position, Is.EqualTo(new Point(500, 200)));
                Assert.That(west.Movement.Position, Is.EqualTo(new Point(100, 200)));
                Assert.That(nearest, Is.GreaterThanOrEqualTo(20 - FieldPath.CellSize - FieldUnit.DefaultStepLength), "they do not walk through each other");

                window.Close();
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [AvaloniaTest]
        public async Task ShowsSmokeAtTheWeaponAndABangWhereTheShotLands()
        {
            var folder = UnitArchiveFolder(out var archive);
            try
            {
                var window = new MainWindow(archive) { SoundPlayer = new RecordingSoundPlayer() };
                window.Show();
                await window.RenderingCompleted;
                window.Pause();
                window.UpdateLayout();

                var unit = OnlyUnit(window);
                var feet = new Point(400, 300);
                unit.Movement.PlaceAt(feet);
                unit.Movement.TurnTo(90); // west
                window.Tick();
                await window.RenderingCompleted;

                // The attack fires its cannon at frame 2: smoke appears at the weapon's point, (1, 1) in the
                // sprite anchored at (30, 15), facing the way the unit faces.
                window.AttackWithSelected();
                window.Tick();
                Assert.That(window.Effects, Is.Empty);
                window.Tick();
                var muzzle = feet + new Vector(1 - 30, 1 - 15);
                var smoke = window.Effects.Single(effect => effect.State.Code == "SMOKE");
                Assert.That(smoke.Movement.Position, Is.EqualTo(muzzle));
                Assert.That(smoke.Direction, Is.EqualTo(4), "west, like the unit");
                await window.RenderingCompleted;
                window.Tick();
                Assert.That(smoke.Image.Source, Is.Not.Null, "it is drawn");

                // The smoke plays once (2 frames) and is gone; the shell is on its way.
                window.Tick();
                var shell = window.Effects.Single();
                Assert.That(shell.State.Code, Is.EqualTo("SHELL"));

                // The shell flies from the weapon to as far west as a shot reaches, in an arc: launched at
                // 45 degrees, it is a quarter of the way high in the middle.
                var landing = feet + new Vector(-300, 0);
                var way = landing - muzzle;
                var highest = 0.0;
                for (var i = 0; i < 30 && window.Effects.Contains(shell); i++)
                {
                    var position = shell.Movement.Position;
                    var done = (position.X - muzzle.X) / way.X;
                    highest = Math.Max(highest, muzzle.Y + way.Y * done - position.Y);
                    window.Tick();
                }

                Assert.That(highest, Is.EqualTo(Math.Sqrt(way.X * way.X + way.Y * way.Y) / 4).Within(2));

                // Where it lands there is a bang.
                var bang = window.Effects.Single();
                Assert.That(bang.State.Code, Is.EqualTo("BANG"));
                Assert.That(bang.Movement.Position.X, Is.EqualTo(landing.X).Within(1e-6));
                Assert.That(bang.Movement.Position.Y, Is.EqualTo(landing.Y).Within(1e-6));
                window.Tick();
                window.Tick();
                Assert.That(window.Effects, Is.Empty);

                // East of the unit the sprite is mirrored, and the weapon's point with it.
                unit.Movement.TurnTo(270);
                window.AttackWithSelected();
                window.Tick();
                window.Tick();
                Assert.That(window.Effects.Single(effect => effect.State.Code == "SMOKE").Movement.Position, Is.EqualTo(feet + new Vector(30 - 1, 1 - 15)));

                window.Close();
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [AvaloniaTest]
        public async Task AShotOnlyHurtsTheUnitsWhereItLands()
        {
            var folder = UnitArchiveFolder(out var archive);
            try
            {
                var window = new MainWindow(archive) { SoundPlayer = new RecordingSoundPlayer() };
                window.Show();
                await window.RenderingCompleted;
                window.Pause();
                window.UpdateLayout();

                // The unit faces west; one bystander stands where a shot lands (300 pixels on), one halfway out
                // of its blast (40 pixels, from the edge of the unit's own room of 10), one just outside, one well
                // short of it.
                var unit = OnlyUnit(window);
                unit.Movement.PlaceAt(new Point(500, 200));
                unit.Movement.TurnTo(90);
                var short_ = window.AddToField("TST", new Point(350, 200));
                var there = window.AddToField("TST", new Point(200, 200));
                var inBlast = window.AddToField("TST", new Point(230, 200));
                var outside = window.AddToField("TST", new Point(200, 228));
                window.Tick();
                await window.RenderingCompleted;

                Click(window, unit.Movement.Position, MouseButton.Left);
                window.AttackWithSelected();
                for (var i = 0; i < 30; i++)
                {
                    window.Tick();
                }

                Assert.That(there.Life, Is.EqualTo(27), "hit, friend or not");
                Assert.That(inBlast.Life, Is.EqualTo(42), "20 pixels out: half the damage (15), less 7 of armor");
                Assert.That(outside.Life, Is.EqualTo(50), "28 pixels up the screen is 56 on the ground: outside");
                Assert.That(short_.Life, Is.EqualTo(50), "the shell flew over it");
                Assert.That(unit.Life, Is.EqualTo(50));

                // An enemy that walks away before the shell lands is missed.
                Click(window, new Point(600, 50), MouseButton.Left);
                await window.SetPlayerColor(NationColorOffset.Red);
                var enemy = window.AddToField("TST", new Point(350, 320));
                window.Tick();
                await window.RenderingCompleted;
                Click(window, unit.Movement.Position, MouseButton.Left);
                Click(window, enemy.Movement.Position, MouseButton.Right);
                for (var i = 0; i < 50 && unit.State.Code != "ATTACK"; i++)
                {
                    window.Tick();
                }

                window.Tick(); // the shell leaves
                enemy.Target = null;
                enemy.Movement.PlaceAt(enemy.Movement.Position + new Vector(0, 60));
                unit.Target = null;
                for (var i = 0; i < 30; i++)
                {
                    window.Tick();
                }

                Assert.That(enemy.Life, Is.EqualTo(50));

                window.Close();
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [AvaloniaTest]
        public async Task AShotReachesAsFarAsItsAttackRadius()
        {
            var folder = UnitArchiveFolder(out var archive);
            try
            {
                var window = new MainWindow(archive) { SoundPlayer = new RecordingSoundPlayer() };
                window.Show();
                await window.RenderingCompleted;
                window.Pause();
                window.UpdateLayout();

                // ATTACK2 reaches 200 pixels on the ground (ATTACK_RADIUS 2 100 200): 200 across the screen,
                // 100 down it.
                var unit = OnlyUnit(window);
                unit.Movement.PlaceAt(new Point(500, 200));
                unit.Movement.TurnTo(90);
                var west = window.AddToField("TST", new Point(300, 200));
                var farWest = window.AddToField("TST", new Point(200, 200));
                var south = window.AddToField("TST", new Point(500, 300));
                window.Tick();
                await window.RenderingCompleted;

                Click(window, unit.Movement.Position, MouseButton.Left);
                window.AttackWithSelected("ATTACK2");
                for (var i = 0; i < 30; i++)
                {
                    window.Tick();
                }

                Assert.That(west.Life, Is.EqualTo(0));
                Assert.That(farWest.Life, Is.EqualTo(50), "where a shot without a radius lands");
                Assert.That(south.Life, Is.EqualTo(50));

                unit.Movement.TurnTo(0);
                window.AttackWithSelected("ATTACK2");
                for (var i = 0; i < 30; i++)
                {
                    window.Tick();
                }

                Assert.That(south.Life, Is.EqualTo(0));

                // Sent against an enemy with ATTACK2 picked: 150 pixels away it is used, but 60 is nearer than
                // its minimum of 100, so the unit makes its other attack.
                Click(window, new Point(600, 50), MouseButton.Left);
                await window.SetPlayerColor(NationColorOffset.Red);
                var enemy = window.AddToField("TST", new Point(650, 200));
                window.Tick();
                await window.RenderingCompleted;
                Click(window, unit.Movement.Position, MouseButton.Left);
                window.FindControl<ComboBox>("AttackBox").SelectedIndex = 1;
                Click(window, enemy.Movement.Position, MouseButton.Right);
                window.Tick();
                Assert.That(unit.State.Code, Is.EqualTo("ATTACK2"));
                Assert.That(unit.Movement.Position, Is.EqualTo(new Point(500, 200)));

                var near = window.AddToField("TST", new Point(500, 170));
                unit.Target = near;
                for (var i = 0; i < 10 && unit.State.Code != "ATTACK"; i++)
                {
                    window.Tick();
                }

                Assert.That(unit.State.Code, Is.EqualTo("ATTACK"));
                Assert.That(unit.Movement.Position, Is.EqualTo(new Point(500, 200)));

                window.Close();
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [AvaloniaTest]
        public async Task PlaysTheAnimationsAroundAttackingRestingAndDying()
        {
            var folder = Path.Combine(Path.GetTempPath(), "viewer-veteran-" + Guid.NewGuid());
            Directory.CreateDirectory(folder);
            foreach (var (name, content) in UnitTests.GpTestFiles.UnitGameData())
            {
                File.WriteAllBytes(Path.Combine(folder, name), content);
            }

            // A unit that brings its weapon up before an attack and lowers it after, rests, and decays in two stages.
            File.WriteAllText(Path.Combine(folder, "VET.MD"), string.Join("\r\n",
                "NAME Veteran",
                "USERLC 0 tsts SHADOW -20 -10",
                "USERLC 1 tstg SHADOW -30 -15",
                "#STAND     9 1 0 0",
                "@MOTION_L  9 1 0 2",
                "@PATTACK   9 1 0 2",
                "#PSTAND    9 1 1 1",
                "@ATTACK    9 1 0 3",
                "@UATTACK   9 1 2 0",
                "@REST      9 1 0 4",
                "@DEATH     9 1 1 3",
                "#DEATHLIE1 9 1 1 3",
                "#DEATHLIE2 9 1 1 2",
                "DAMAGE 0 30",
                "ATTACK_PAUSE 0 3",
                "LIFE 500",
                ""));

            try
            {
                var window = new MainWindow(folder) { SoundPlayer = new RecordingSoundPlayer() };
                window.Show();
                await window.RenderingCompleted;
                window.Pause();
                window.UpdateLayout();
                window.RemoveSelectedFromField();

                var veteran = window.AddToField("VET", new Point(300, 200));
                string[] StatesOver(int ticks)
                {
                    var states = new System.Collections.Generic.List<string>();
                    for (var i = 0; i < ticks; i++)
                    {
                        window.Tick();
                        if (states.LastOrDefault() != veteran.State.Code)
                        {
                            states.Add(veteran.State.Code);
                        }
                    }

                    return states.ToArray();
                }

                // An attack on nobody: weapon up, attack, weapon down, at ease.
                window.AttackWithSelected();
                Assert.That(veteran.State.Code, Is.EqualTo("PATTACK"));
                Assert.That(StatesOver(10), Is.EqualTo(new[] { "PATTACK", "ATTACK", "UATTACK", "STAND" }));

                // Against an enemy it keeps the weapon up between attacks (PSTAND while it rests for its pause),
                // and lowers it when the enemy has fallen.
                Click(window, new Point(600, 50), MouseButton.Left);
                await window.SetPlayerColor(NationColorOffset.Red);
                var enemy = window.AddToField("TST", new Point(330, 200));
                enemy.StandsGround = true;
                Click(window, veteran.Movement.Position, MouseButton.Left);
                Click(window, enemy.Movement.Position, MouseButton.Right);
                var fight = StatesOver(40);
                Assert.That(enemy.IsDead, Is.True);
                Assert.That(fight.Take(4), Is.EqualTo(new[] { "PATTACK", "ATTACK", "PSTAND", "ATTACK" }));
                Assert.That(fight.Count(state => state == "PATTACK"), Is.EqualTo(1), "the weapon stays up between attacks");
                Assert.That(fight.TakeLast(2), Is.EqualTo(new[] { "UATTACK", "STAND" }));

                // Left alone it takes a rest now and then.
                Assert.That(StatesOver(450), Does.Contain("REST"));

                // Killed, it falls, lies, decays to its second stage and is gone.
                Click(window, veteran.Movement.Position, MouseButton.Left);
                window.KillSelected();
                var death = StatesOver(250);
                Assert.That(death, Is.EqualTo(new[] { "DEATH", "DEATHLIE1", "DEATHLIE2" }));
                Assert.That(window.FieldUnits, Has.No.Member(veteran));

                window.Close();
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        private sealed class RecordingSoundPlayer : ISoundPlayer
        {
            public System.Collections.Generic.List<string> Played { get; } = new System.Collections.Generic.List<string>();

            public void Play(string path) => Played.Add(path);
        }

        [AvaloniaTest]
        public async Task SelectsAndWalksAUnitOnTheField()
        {
            var folder = UnitArchiveFolder(out var archive);
            try
            {
                // The first unit stands in the middle of the field, selected, and the field runs.
                var window = new MainWindow(archive);
                window.Show();
                await window.RenderingCompleted;
                window.Pause(); // the test drives the ticks
                window.UpdateLayout();
                window.Tick();
                await window.RenderingCompleted;

                Assert.That(window.Title, Is.EqualTo("GP Viewer - All.gsc"));
                Assert.That(ViewerSettings.LastGameData, Is.EqualTo(archive), "remembered for the next start");
                var unit = OnlyUnit(window);
                Assert.That(unit.State.Code, Is.EqualTo("STAND"));
                Assert.That(unit.IsSelected, Is.True);
                Assert.That(unit.Direction, Is.EqualTo(0), "faces the viewer");
                Assert.That(DrawnColumnsAtFeet(unit), Is.EqualTo(new[] { 0 }), "feet at the anchor");

                // A click on empty field deselects; right-click then does nothing.
                var start = unit.Movement.Position;
                Click(window, start + new Vector(60, 60), MouseButton.Left);
                Assert.That(unit.IsSelected, Is.False);
                Click(window, start + new Vector(0, -100), MouseButton.Right);
                Assert.That(unit.Movement.IsWalking, Is.False);

                // Selected and sent north: it turns and walks.
                Click(window, start, MouseButton.Left);
                Assert.That(unit.IsSelected, Is.True);
                Click(window, start + new Vector(0, -100), MouseButton.Right);
                await window.RenderingCompleted;
                Assert.That(unit.Movement.IsWalking, Is.True);
                Assert.That(unit.State.Code, Is.EqualTo("MOTION_L+MOTION_R"));
                Assert.That(unit.Direction, Is.EqualTo(8), "north");

                for (var i = 0; i < 10; i++)
                {
                    window.Tick();
                }

                Assert.That(unit.Movement.Position, Is.EqualTo(start + new Vector(0, -10 * FieldUnit.DefaultStepLength)));

                // On arrival it stands again, still facing north.
                for (var i = 0; i < 15; i++)
                {
                    window.Tick();
                }

                await window.RenderingCompleted;
                Assert.That(unit.Movement.IsWalking, Is.False);
                Assert.That(unit.Movement.Position, Is.EqualTo(start + new Vector(0, -100)));
                Assert.That(unit.State.Code, Is.EqualTo("STAND"));
                Assert.That(unit.Direction, Is.EqualTo(8));

                // Sent east: the mirrored frames are drawn with the feet at the unit's position too.
                Click(window, unit.Movement.Position + new Vector(80, 0), MouseButton.Right);
                Assert.That(unit.Direction, Is.EqualTo(12), "east");
                window.Tick();
                await window.RenderingCompleted;
                window.Tick();
                Assert.That(DrawnColumnsAtFeet(unit), Is.EqualTo(new[] { -5, -4, -3, -2, -1 }));

                // Turning while walking keeps the step it is in.
                var pose = unit.Pose;
                Click(window, unit.Movement.Position + new Vector(-50, 50), MouseButton.Right);
                Assert.That(unit.Direction, Is.EqualTo(2), "south-west");
                Assert.That(unit.Pose, Is.EqualTo(pose));

                window.Close();
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [AvaloniaTest]
        public async Task SelectsSeveralUnitsWithABoxAndSendsThemTogether()
        {
            var folder = UnitArchiveFolder(out var archive);
            try
            {
                var window = new MainWindow(archive);
                window.Show();
                await window.RenderingCompleted;
                window.Pause();
                window.UpdateLayout();

                // Two more units; the color picker recolors the selected unit and colors the ones added next.
                var first = OnlyUnit(window);
                first.Movement.PlaceAt(new Point(100, 200));
                // Picking a unit in the list puts one on the field; the list clears, so it can be picked again.
                var unitBox = window.FindControl<ComboBox>("UnitBox");
                unitBox.SelectedIndex = 0;
                Assert.That(unitBox.SelectedItem, Is.Null);
                var second = window.FieldUnits[1];
                Assert.That(second.Kind.Descriptor.FileName, Is.EqualTo("TST"));
                second.Movement.PlaceAt(new Point(150, 210));
                await window.SetPlayerColor(NationColorOffset.Red);
                var third = window.AddToField("TST", new Point(400, 200));
                Assert.That(window.FieldUnits, Has.Count.EqualTo(3));
                Assert.That(window.SelectedFieldUnits, Is.EqualTo(new[] { third }), "a new unit is selected");
                Assert.That(second.Color.Offset, Is.EqualTo(NationColorOffset.Red.Offset));
                Assert.That(third.Color.Offset, Is.EqualTo(NationColorOffset.Red.Offset));
                Assert.That(first.Color.Offset, Is.Not.EqualTo(NationColorOffset.Red.Offset), "only the selected unit is recolored");
                window.Tick();
                await window.RenderingCompleted;

                // A box around the first two selects them (and only them).
                Drag(window, new Point(80, 180), new Point(170, 230));
                Assert.That(window.SelectedFieldUnits, Is.EqualTo(new[] { first, second }));
                Assert.That(window.FindControl<Avalonia.Controls.Shapes.Rectangle>("SelectionRectangle").IsVisible, Is.False);

                // Shift-click adds the third; a click on empty field deselects; a shift-drag box adds too.
                Click(window, third.Movement.Position, MouseButton.Left, RawInputModifiers.Shift);
                Assert.That(window.SelectedFieldUnits, Has.Count.EqualTo(3));
                Click(window, new Point(300, 50), MouseButton.Left);
                Assert.That(window.SelectedFieldUnits, Is.Empty);
                Drag(window, new Point(80, 180), new Point(120, 230));
                Drag(window, new Point(380, 180), new Point(420, 230), RawInputModifiers.Shift);
                Assert.That(window.SelectedFieldUnits, Is.EqualTo(new[] { first, third }));
                Click(window, second.Movement.Position, MouseButton.Left, RawInputModifiers.Shift);

                // All three are sent north together: each walks to its own place in a block around the point.
                var target = new Point(250, 60);
                Click(window, target, MouseButton.Right);
                var units = new[] { first, second, third };
                Assert.That(units.Select(u => u.Movement.IsWalking), Is.All.True);
                Assert.That(units.Select(u => u.State.Code), Is.All.EqualTo("MOTION_L+MOTION_R"));
                var places = units.Select(u => u.Movement.Target.Value).ToList();
                Assert.That(places.Distinct().Count(), Is.EqualTo(3), "no two units go to the same place");
                Assert.That(places.Average(p => p.X), Is.EqualTo(target.X).Within(40));
                Assert.That(units[0].Direction, Is.Not.EqualTo(units[2].Direction), "each heads for its own place");
                foreach (var u in units)
                {
                    Assert.That(u.Movement.Heading, Is.EqualTo(FieldUnit.HeadingOf(u.Movement.Target.Value - u.Movement.Position)).Within(1e-9));
                }

                for (var i = 0; i < 200 && units.Any(u => u.Movement.IsWalking); i++)
                {
                    window.Tick();
                }

                await window.RenderingCompleted;
                Assert.That(units.Select(u => u.State.Code), Is.All.EqualTo("STAND"));
                Assert.That(units.Select(u => u.Movement.Position), Is.EqualTo(places));

                // Remove takes the selected units off the field.
                Click(window, third.Movement.Position, MouseButton.Left);
                window.FindControl<Button>("RemoveUnitsButton").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Assert.That(window.FieldUnits, Is.EqualTo(new[] { first, second }));

                // Opening other game data starts the field over.
                await window.LoadFile(archive);
                Assert.That(window.FieldUnits, Has.Count.EqualTo(2));
                Assert.That(window.FieldUnits, Has.None.SameAs(first));

                window.Close();
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [AvaloniaTest]
        public Task AUnitWalksAsFastAsItsGeometrySaysAndTakesItsRoom()
        {
            // A step of 64 is twice a foot soldier's 32.
            return OnFieldWith(new[] { "GEOMETRY 1 15 64" }, async (window, unit) =>
            {
                var start = unit.Movement.Position;
                Click(window, start + new Vector(200, 0), MouseButton.Right);
                for (var i = 0; i < 5; i++)
                {
                    window.Tick();
                }

                Assert.That(unit.Movement.Position, Is.EqualTo(start + new Vector(5 * 2 * FieldUnit.DefaultStepLength, 0)));

                // Sent to where another unit stands, it stops where its room (a radius of 15) and the other's meet.
                unit.Movement.PlaceAt(start);
                var other = window.AddToField("TST", start + new Vector(100, 0));
                Click(window, start, MouseButton.Left);
                window.Tick();
                await window.RenderingCompleted;
                Click(window, other.Movement.Position, MouseButton.Right);
                for (var i = 0; i < 40; i++)
                {
                    window.Tick();
                }

                Assert.That(unit.Movement.IsWalking, Is.False);
                Assert.That(
                    FieldPath.GroundDistance(other.Movement.Position, unit.Movement.Position),
                    Is.InRange(30 - FieldPath.CellSize, 30 + 2 * FieldPath.CellSize));
            });
        }

        [AvaloniaTest]
        public Task ASlowUnitTurnsWhereItStandsBeforeItWalksOrFires()
        {
            // ROTATE 16 is a sixteenth of a turn (22.5 degrees) a tick.
            return OnFieldWith(new[] { "ROTATE 16" }, async (window, unit) =>
            {
                // Facing south and sent east: a quarter turn takes 4 ticks, on the spot.
                var start = unit.Movement.Position;
                Click(window, start + new Vector(100, 0), MouseButton.Right);
                Assert.That(unit.Movement.Heading, Is.EqualTo(0));
                for (var i = 1; i <= 4; i++)
                {
                    window.Tick();
                    Assert.That(unit.Movement.Position, Is.EqualTo(start));
                    Assert.That(unit.Movement.Heading, Is.EqualTo(360 - 22.5 * i));
                    Assert.That(unit.State.Code, Is.EqualTo("STAND"));
                }

                window.Tick();
                Assert.That(unit.State.Code, Is.EqualTo("MOTION_L+MOTION_R"));
                Assert.That(unit.Movement.Position, Is.EqualTo(start + new Vector(FieldUnit.DefaultStepLength, 0)));

                // Sent against an enemy to its west, it turns half round (8 ticks) before it fires.
                unit.Movement.PlaceAt(start);
                var enemy = await AddEnemy(window, start + new Vector(-100, 0));
                Click(window, enemy.Movement.Position, MouseButton.Right);
                for (var i = 0; i < 8; i++)
                {
                    window.Tick();
                    Assert.That(unit.State.Code, Is.EqualTo("STAND"));
                }

                window.Tick();
                Assert.That(unit.Movement.Heading, Is.EqualTo(90));
                Assert.That(unit.State.Code, Is.EqualTo("ATTACK"));
            });
        }

        [AvaloniaTest]
        public Task AnIdleUnitTakesOnAnEnemyWithinItsSearchRadiusUnlessItStandsGround()
        {
            return OnFieldWith(new[] { "SEARCH_ENEMY_RADIUS 350", "STANDGROUND" }, async (window, unit) =>
            {
                // 180 pixels up the screen is 360 on the ground, too far to notice; 320 is not, and beyond the
                // shot's reach of 300.
                var far = await AddEnemy(window, unit.Movement.Position + new Vector(0, -180));
                window.Tick();
                Assert.That(unit.Target, Is.Null);
                Assert.That(far.Target, Is.Null);

                var near = await AddEnemy(window, unit.Movement.Position + new Vector(-320, 0));
                window.Tick();
                Assert.That(unit.Target, Is.SameAs(near));
                Assert.That(unit.Movement.IsWalking, Is.True);
                Assert.That(near.Target, Is.SameAs(unit));

                // Holding its place it lets the enemy be, until it has come within reach.
                var box = window.FindControl<CheckBox>("StandGroundBox");
                Assert.That(box.IsEnabled, Is.True);
                Assert.That(box.IsChecked, Is.False);
                window.StandGroundWithSelected(true);
                Assert.That(box.IsChecked, Is.True);
                near.Target = null;
                near.Movement.PlaceAt(near.Movement.Position);
                var held = unit.Movement.Position;
                window.Tick();
                Assert.That(unit.Target, Is.Null);
                Assert.That(unit.Movement.IsWalking, Is.False);

                near.Movement.PlaceAt(held + new Vector(-250, 0));
                near.StandsGround = true;
                window.Tick();
                Assert.That(unit.Target, Is.SameAs(near));
                Assert.That(unit.Movement.Position, Is.EqualTo(held));
                Assert.That(unit.State.Code, Is.EqualTo("ATTACK"));

                // Sent against an enemy out of reach, it goes all the same.
                unit.Target = null;
                Click(window, far.Movement.Position, MouseButton.Right);
                for (var i = 0; i < 5; i++)
                {
                    window.Tick();
                }

                Assert.That(unit.Target, Is.SameAs(far));
                Assert.That(unit.Movement.IsWalking, Is.True);
            });
        }

        [AvaloniaTest]
        public Task AUnitChoosesTheAttackWhoseDetectionRadiusTheEnemyIsIn()
        {
            // The cannon (attack 0) is for 150 pixels and more, the mortar (attack 2, which reaches 100 to 200) for less.
            var lines = new[] { "DET_RADIUS 0 0 50", "DET_RADIUS 0 150 9000", "DET_RADIUS 2 0 150", "DAMAGE 2 1" };
            return OnFieldWith(lines, async (window, unit) =>
            {
                var enemy = await AddEnemy(window, unit.Movement.Position + new Vector(120, 0));
                Click(window, enemy.Movement.Position, MouseButton.Right);
                window.Tick();
                Assert.That(unit.State.Code, Is.EqualTo("ATTACK2"));

                enemy.Movement.PlaceAt(unit.Movement.Position + new Vector(180, 0));
                for (var i = 0; i < 10 && unit.State.Code != "ATTACK"; i++)
                {
                    window.Tick();
                }

                Assert.That(unit.State.Code, Is.EqualTo("ATTACK"));
            });
        }

        [AvaloniaTest]
        public Task ShotsScatterAroundTheirAimAndHitATargetAtItsHeight()
        {
            // At 200 pixels a scatter of 512 (per 2048) is 50 pixels; the unit is 30 pixels high.
            return OnFieldWith(new[] { "RAZBROS 512", "ZPOINTS 30 30" }, async (window, unit) =>
            {
                unit.Movement.TurnTo(90);
                var aim = unit.Movement.Position + new Vector(-300, 0);
                var landings = new System.Collections.Generic.List<Point>();
                for (var shot = 0; shot < 20; shot++)
                {
                    window.AttackWithSelected();
                    for (var i = 0; i < 6; i++)
                    {
                        window.Tick();
                        landings.AddRange(window.Effects.Where(effect => effect.Flight != null).Select(effect => effect.Flight.Value.To));
                    }
                }

                Assert.That(landings, Is.Not.Empty);
                Assert.That(landings.Select(landing => FieldPath.GroundDistance(landing, aim)), Is.All.LessThanOrEqualTo(300 * 512 / 2048.0 + 1e-6));
                Assert.That(landings.Distinct().Count(), Is.GreaterThan(5));
            });
        }

        [AvaloniaTest]
        public Task AShotThatLandsOnItsTargetFliesToItsHeight()
        {
            return OnFieldWith(new[] { "ZPOINTS 30 30" }, async (window, unit) =>
            {
                var enemy = await AddEnemy(window, unit.Movement.Position + new Vector(-200, 0));
                enemy.StandsGround = true;
                Click(window, enemy.Movement.Position, MouseButton.Right);
                FieldUnitView shell = null;
                for (var i = 0; i < 10 && shell == null; i++)
                {
                    window.Tick();
                    shell = window.Effects.FirstOrDefault(effect => effect.Flight != null);
                }

                Assert.That(shell, Is.Not.Null);
                Assert.That(shell.Flight.Value.To, Is.EqualTo(enemy.Movement.Position + new Vector(0, -30)));
            });
        }

        [AvaloniaTest]
        public Task AUnitThatShowsItsDelayHasABarWhileItRests()
        {
            return OnFieldWith(new[] { "SHOWDELAY" }, (window, unit) =>
            {
                // The attack has 3 frames and a rest of 2 ticks after it.
                window.AttackWithSelected();
                Assert.That(unit.ReloadBar.IsVisible, Is.False);
                window.Tick();
                window.Tick();
                window.Tick();
                Assert.That(unit.PauseTicksLeft, Is.EqualTo(2));
                Assert.That(unit.ReloadBar.IsVisible, Is.True);
                Assert.That(unit.ReloadBar.Width, Is.EqualTo(1), "nothing of the rest is over");

                window.Tick();
                Assert.That(unit.ReloadBar.Width, Is.EqualTo(15), "half of it");
                window.Tick();
                Assert.That(unit.ReloadBar.IsVisible, Is.False);
                return Task.CompletedTask;
            });
        }

        /// <summary>The first of the units the field starts with, the others taken off it.</summary>
        private static FieldUnitView OnlyUnit(MainWindow window)
        {
            foreach (var other in window.FieldUnits.Skip(1).ToList())
            {
                window.RemoveFromField(other);
            }

            return window.FieldUnits.Single();
        }

        private static string UnitArchiveFolder(out string archive, params string[] unitLines)
        {
            var folder = Path.Combine(Path.GetTempPath(), "viewer-field-" + Guid.NewGuid());
            Directory.CreateDirectory(folder);
            archive = Path.Combine(folder, "All.gsc");
            File.WriteAllBytes(archive, UnitTests.GpTestFiles.GameArchive(UnitTests.GpTestFiles.UnitGameData(unitLines)));
            return folder;
        }

        /// <summary>
        /// Runs a test on the field of a paused window whose unit has more descriptor lines: one unit of the
        /// player's is on it, selected.
        /// </summary>
        private static async Task OnFieldWith(string[] unitLines, Func<MainWindow, FieldUnitView, Task> test)
        {
            var folder = UnitArchiveFolder(out var archive, unitLines);
            try
            {
                var window = new MainWindow(archive) { SoundPlayer = new RecordingSoundPlayer() };
                window.Show();
                await window.RenderingCompleted;
                window.Pause();
                window.UpdateLayout();

                var unit = OnlyUnit(window);
                unit.Movement.PlaceAt(new Point(400, 200));
                window.Tick();
                await window.RenderingCompleted;

                await test(window, unit);
                window.Close();
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <summary>Puts an enemy of the player's units on the field, leaving the selection as it is.</summary>
        private static async Task<FieldUnitView> AddEnemy(MainWindow window, Point at)
        {
            // Deselected first, so the selection keeps its color.
            var selected = window.SelectedFieldUnits;
            Click(window, new Point(700, 20), MouseButton.Left);
            await window.SetPlayerColor(NationColorOffset.Red);
            var enemy = window.AddToField("TST", at);
            Click(window, new Point(700, 20), MouseButton.Left);
            foreach (var unit in selected)
            {
                Click(window, unit.Movement.Position, MouseButton.Left, RawInputModifiers.Shift);
            }

            return enemy;
        }

        private static Point OnWindow(MainWindow window, Point fieldPoint) =>
            window.FindControl<Canvas>("Field").TranslatePoint(fieldPoint, window).Value;

        private static void Click(MainWindow window, Point fieldPoint, MouseButton button, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            var point = OnWindow(window, fieldPoint);
            window.MouseDown(point, button, modifiers);
            window.MouseUp(point, button, modifiers);
        }

        private static void Drag(MainWindow window, Point from, Point to, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            window.MouseDown(OnWindow(window, from), MouseButton.Left, modifiers);
            window.MouseMove(OnWindow(window, (from + to) / 2), modifiers | RawInputModifiers.LeftMouseButton);
            window.MouseMove(OnWindow(window, to), modifiers | RawInputModifiers.LeftMouseButton);
            window.MouseUp(OnWindow(window, to), MouseButton.Left, modifiers);
        }

        /// <summary>Columns of the unit's row (its feet) that are drawn on the field, relative to the feet.</summary>
        private static int[] DrawnColumnsAtFeet(FieldUnitView unit)
        {
            var image = unit.Image;
            var bitmap = (WriteableBitmap)image.Source;
            var feet = unit.Movement.Position;
            var left = (int)Canvas.GetLeft(image);
            var y = (int)Math.Round(feet.Y) - (int)Canvas.GetTop(image);
            var width = bitmap.PixelSize.Width;
            var row = new byte[width * 4];

            using (var buffer = bitmap.Lock())
            {
                Marshal.Copy(buffer.Address + y * buffer.RowBytes, row, 0, row.Length);
            }

            return Enumerable.Range(0, width)
                .Where(x => row[4 * x + 3] != 0)
                .Select(x => left + x - (int)Math.Round(feet.X))
                .ToArray();
        }
    }
}
