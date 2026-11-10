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
                Assert.That(window.FieldUnits.Single().Kind.Descriptor.FileName, Is.EqualTo("TST"));
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

                var unit = window.FieldUnits.Single();
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
                var unit = window.FieldUnits.Single();
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
                var unit = window.FieldUnits.Single();
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
                var first = window.FieldUnits.Single();
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
                Assert.That(window.FieldUnits, Has.Count.EqualTo(1));
                Assert.That(window.FieldUnits, Has.None.SameAs(first));

                window.Close();
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        private static string UnitArchiveFolder(out string archive)
        {
            var folder = Path.Combine(Path.GetTempPath(), "viewer-field-" + Guid.NewGuid());
            Directory.CreateDirectory(folder);
            archive = Path.Combine(folder, "All.gsc");
            File.WriteAllBytes(archive, UnitTests.GpTestFiles.GameArchive(UnitTests.GpTestFiles.UnitGameData()));
            return folder;
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
