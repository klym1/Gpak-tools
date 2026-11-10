using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
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
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont();
    }

    public class MainWindowTests
    {
        // Image 0 of gp/test.gp as the viewer shows it: on a gray background
        // (BGRA bytes).
        private const string ExpectedFirstImage = "576x467:E749EB9D4F20DC75FF810C98258F00554A63BA0667E695BEB49135D3738762DF";

        [AvaloniaTest]
        public async Task ShowsAllImagesOfTheFile()
        {
            var window = new MainWindow();
            window.Show();
            await window.RenderingCompleted;

            var image = window.FindControl<Image>("ImageView");
            var status = window.FindControl<TextBlock>("StatusText");

            Assert.That(window.Title, Is.EqualTo("GP Viewer - test.gp"));
            Assert.That(window.FrameCount, Is.EqualTo(15));
            Assert.That(window.CurrentFrame, Is.EqualTo(0));
            Assert.That(window.FindControl<TextBlock>("FrameText").Text, Is.EqualTo("Frame 0 / 14"));
            Assert.That(window.FindControl<ListBox>("ImageList"), Is.Null, "the frame list is gone");
            Assert.That(status.Text, Is.Empty, "every frame rendered");
            Assert.That(window.FindControl<TextBox>("LogBox"), Is.Null, "the render log is gone");

            var bitmap = (WriteableBitmap)image.Source;
            Assert.That(HashOf(bitmap), Is.EqualTo(ExpectedFirstImage));

            // Selecting another entry shows that image, on the same canvas size as the largest frame.
            window.ShowFrameAt(9);
            Assert.That(((Bitmap)image.Source).PixelSize, Is.EqualTo(new PixelSize(576, 467)));
            Assert.That(HashOf((WriteableBitmap)image.Source), Is.Not.EqualTo(ExpectedFirstImage));

            // Keep a screenshot of the window as a test artifact.
            window.ShowFrameAt(1);
            var frame = window.CaptureRenderedFrame();
            Assert.That(frame, Is.Not.Null);
            var screenshot = Path.Combine(TestContext.CurrentContext.WorkDirectory, "viewer.png");
            frame.Save(screenshot, PngBitmapEncoderOptions.Default);
            TestContext.AddTestAttachment(screenshot);

            window.Close();
        }

        [AvaloniaTest]
        public async Task ReportsMissingFile()
        {
            var window = new MainWindow(Path.Combine(Path.GetTempPath(), "does-not-exist.gp"));
            window.Show();
            await window.RenderingCompleted;

            Assert.That(window.FindControl<TextBlock>("StatusText").Text, Does.StartWith("Failed to open"));
            Assert.That(window.FrameCount, Is.EqualTo(0));

            window.Close();
        }

        [AvaloniaTest]
        public async Task OpensAnotherFile()
        {
            var copy = Path.Combine(Path.GetTempPath(), "viewer-test-" + Guid.NewGuid() + ".gp");
            File.Copy(MainWindow.DefaultGpFile, copy);

            try
            {
                var window = new MainWindow();
                window.Show();
                await window.RenderingCompleted;

                window.FindControl<NumericUpDown>("FromFrame").Value = 3;
                window.Play();

                await window.LoadFile(copy);

                Assert.That(window.CurrentFile, Is.EqualTo(copy));
                Assert.That(window.Title, Is.EqualTo("GP Viewer - " + Path.GetFileName(copy)));
                Assert.That(window.FindControl<TextBlock>("FileNameText").Text, Does.EndWith("(15 frames)"));
                Assert.That(window.IsPlaying, Is.False, "loading a file stops playback");
                Assert.That(window.CurrentFrame, Is.EqualTo(0));
                Assert.That(window.FindControl<NumericUpDown>("FromFrame").Value, Is.EqualTo(0), "the loop range is reset");
                Assert.That(window.FindControl<NumericUpDown>("ToFrame").Value, Is.EqualTo(14));

                window.Close();
            }
            finally
            {
                File.Delete(copy);
            }
        }

        [AvaloniaTest]
        public async Task StepsThroughTheLoopRangeAndWrapsAround()
        {
            var window = new MainWindow();
            window.Show();
            await window.RenderingCompleted;

            window.FindControl<NumericUpDown>("FromFrame").Value = 4;
            window.FindControl<NumericUpDown>("ToFrame").Value = 6;

            // Frame 0 is outside the range: stepping starts at the first frame of the range.
            window.StepFrame(1);
            Assert.That(window.CurrentFrame, Is.EqualTo(4));
            window.StepFrame(1);
            window.StepFrame(1);
            Assert.That(window.CurrentFrame, Is.EqualTo(6));
            window.StepFrame(1);
            Assert.That(window.CurrentFrame, Is.EqualTo(4));
            window.StepFrame(-1);
            Assert.That(window.CurrentFrame, Is.EqualTo(6));

            window.Close();
        }

        [AvaloniaTest]
        public async Task PlaysTheAnimation()
        {
            var window = new MainWindow();
            window.Show();
            await window.RenderingCompleted;

            window.FindControl<NumericUpDown>("FramesPerSecond").Value = 60;
            window.Play();
            Assert.That(window.IsPlaying, Is.True);
            Assert.That(window.FindControl<Button>("PlayButton").Content, Is.EqualTo("⏸ Pause"));

            // Let the dispatcher timer run until the animation has advanced a few frames.
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (window.CurrentFrame < 3 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
            }

            Assert.That(window.CurrentFrame, Is.GreaterThanOrEqualTo(3));

            window.Pause();
            var stoppedAt = window.CurrentFrame;
            await Task.Delay(100);
            Assert.That(window.CurrentFrame, Is.EqualTo(stoppedAt));
            Assert.That(window.IsPlaying, Is.False);

            window.Close();
        }

        [AvaloniaTest]
        public async Task ChangesThePlayerColorWithoutLosingThePosition()
        {
            var window = new MainWindow();
            window.Show();
            await window.RenderingCompleted;

            var image = window.FindControl<Image>("ImageView");
            var colors = window.FindControl<ComboBox>("PlayerColorBox");

            Assert.That(colors.ItemCount, Is.EqualTo(7));
            Assert.That(((PlayerColorOption)colors.SelectedItem).Name, Is.EqualTo("Blue"));

            // Frame 9 of test.gp has player color parts.
            window.FindControl<NumericUpDown>("FromFrame").Value = 8;
            window.ShowFrameAt(9);
            var blue = HashOf((WriteableBitmap)image.Source);
            window.Play();

            await window.SetPlayerColor(NationColorOffset.Red);

            Assert.That(window.PlayerColor.Offset, Is.EqualTo(NationColorOffset.Red.Offset));
            Assert.That(window.IsPlaying, Is.True, "playback continues");
            Assert.That(window.FindControl<NumericUpDown>("FromFrame").Value, Is.EqualTo(8), "the loop range is kept");

            window.Pause();
            var frame = window.CurrentFrame;
            Assert.That(frame, Is.InRange(8, 14), "still within the loop");

            await window.SetPlayerColor(NationColorOffset.Blue);
            window.ShowFrameAt(9);
            Assert.That(HashOf((WriteableBitmap)image.Source), Is.EqualTo(blue));

            await window.SetPlayerColor(NationColorOffset.Red);
            Assert.That(window.CurrentFrame, Is.EqualTo(9), "the shown frame is kept");
            Assert.That(HashOf((WriteableBitmap)image.Source), Is.Not.EqualTo(blue));

            window.Close();
        }

        [AvaloniaTest]
        public async Task PlaysOneDirectionAndMirrorsTheEasternHalf()
        {
            var folder = Path.Combine(Path.GetTempPath(), "viewer-directions-" + Guid.NewGuid());
            Directory.CreateDirectory(folder);
            var sprite = Path.Combine(folder, "UNIT.GP");
            File.WriteAllBytes(sprite, UnitTests.GpTestFiles.Unit(poses: 3, anchorX: 20));
            File.WriteAllText(Path.Combine(folder, "UNIT.MD"), "USERLC 0 unit  SHADOW -20 -10\r\n#MOTION_L  9 3  0 0 0 1 0 2\r\n");

            try
            {
                var window = new MainWindow(sprite);
                window.Show();
                await window.RenderingCompleted;

                    var image = window.FindControl<Image>("ImageView");

                Assert.That(window.FindControl<StackPanel>("DirectionPanel").IsVisible, Is.True);
                Assert.That(window.FindControl<ComboBox>("DirectionBox").ItemCount, Is.EqualTo(16));
                Assert.That(window.FindControl<TextBlock>("FileNameText").Text, Does.EndWith("(3 poses × 16 directions)"));
                Assert.That(window.Directions.Source, Is.EqualTo(GpDirectionsSource.Descriptor));
                Assert.That(window.FrameCount, Is.EqualTo(3), "one image per pose");
                Assert.That(window.FindControl<TextBlock>("FrameText").Text, Is.EqualTo("Pose 0 / 2"));
                Assert.That(window.CurrentDirection, Is.EqualTo(0));

                // The canvas is centred on the anchor (x = 20 of a 40 pixel wide canvas).
                // South: 1 pixel at the anchor.
                Assert.That(DrawnColumns(image), Is.EqualTo(new[] { 20 }));

                // Changing direction while playing keeps playing.
                window.Play();
                await window.SetDirection(4);
                Assert.That(window.IsPlaying, Is.True);
                window.Pause();

                // West is stored: 5 pixels to the right of the anchor.
                window.ShowFrameAt(1);
                var pose = window.CurrentFrame;
                Assert.That(DrawnColumns(image), Is.EqualTo(new[] { 20, 21, 22, 23, 24 }));

                // East is west mirrored around the anchor.
                await window.SetDirection(12);
                Assert.That(window.CurrentFrame, Is.EqualTo(pose), "the pose is kept");
                Assert.That(DrawnColumns(image), Is.EqualTo(new[] { 15, 16, 17, 18, 19 }));

                await window.SetDirection(4);
                Assert.That(DrawnColumns(image), Is.EqualTo(new[] { 20, 21, 22, 23, 24 }));

                // All frames in file order.
                await window.SetShowByDirection(false);
                Assert.That(window.FrameCount, Is.EqualTo(27));
                Assert.That(window.CurrentDirection, Is.EqualTo(-1));
                Assert.That(window.FindControl<NumericUpDown>("ToFrame").Value, Is.EqualTo(26));

                window.Close();
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [AvaloniaTest]
        public async Task HidesTheDirectionPickerForSpritesWithoutDirections()
        {
            var window = new MainWindow();
            window.Show();
            await window.RenderingCompleted;

            Assert.That(window.Directions, Is.Null);
            Assert.That(window.FindControl<StackPanel>("DirectionPanel").IsVisible, Is.False);
            Assert.That(window.CurrentDirection, Is.EqualTo(-1));

            window.Close();
        }

        [AvaloniaTest]
        public async Task BrowsesUnitsOfAGameArchive()
        {
            var folder = Path.Combine(Path.GetTempPath(), "viewer-units-" + Guid.NewGuid());
            Directory.CreateDirectory(folder);
            var archive = Path.Combine(folder, "All.gsc");
            File.WriteAllBytes(archive, UnitTests.GpTestFiles.GameArchive(UnitTests.GpTestFiles.UnitGameData()));

            try
            {
                var window = new MainWindow(archive);
                window.Show();
                await window.RenderingCompleted;

                var image = window.FindControl<Image>("ImageView");

                Assert.That(window.FindControl<StackPanel>("UnitPanel").IsVisible, Is.True);
                Assert.That(window.FindControl<ComboBox>("UnitBox").ItemCount, Is.EqualTo(1), "only units, not buildings");
                Assert.That(window.CurrentUnit.Label, Is.EqualTo("Тестовый воин (TST)"));
                Assert.That(window.CurrentState.Code, Is.EqualTo("MOTION_L+MOTION_R"), "starts with walking");
                Assert.That(window.FrameCount, Is.EqualTo(4));
                Assert.That(window.FindControl<TextBlock>("FileNameText").Text, Is.EqualTo("All.gsc"));
                Assert.That(window.Title, Is.EqualTo("GP Viewer - All.gsc - Тестовый воин (TST)"));
                Assert.That(window.Directions.Count, Is.EqualTo(16));
                Assert.That(window.FindControl<CheckBox>("ByDirectionBox").IsVisible, Is.False);
                Assert.That(window.FindControl<TextBlock>("DirectionLabel").IsVisible, Is.True);

                // Every sprite is placed with its anchor at x = 9 of the canvas, whatever its own anchor.
                Assert.That(DrawnColumns(image), Is.EqualTo(new[] { 9 }), "south: 1 pixel at the anchor");
                await window.SetDirection(4);
                Assert.That(DrawnColumns(image), Is.EqualTo(new[] { 9, 10, 11, 12, 13 }), "west");
                await window.SetDirection(12);
                Assert.That(DrawnColumns(image), Is.EqualTo(new[] { 4, 5, 6, 7, 8 }), "east, mirrored");

                // Another state uses another sprite (anchored at (20, 10) instead of (30, 15)): same place.
                window.Play();
                await window.SelectState("STAND");
                Assert.That(window.FrameCount, Is.EqualTo(1));
                Assert.That(window.CurrentDirection, Is.EqualTo(12), "the direction is kept");
                Assert.That(DrawnColumns(image), Is.EqualTo(new[] { 4, 5, 6, 7, 8 }));

                await window.SetPlayerColor(NationColorOffset.Red);
                Assert.That(window.CurrentState.Code, Is.EqualTo("STAND"), "the state is kept");

                // Opening a sprite without game data around it leaves unit mode.
                window.Pause();
                await window.LoadFile(MainWindow.DefaultGpFile);
                Assert.That(window.CurrentUnit, Is.Null);
                Assert.That(window.Catalog, Is.Null);
                Assert.That(window.FindControl<StackPanel>("UnitPanel").IsVisible, Is.False);
                Assert.That(window.FrameCount, Is.EqualTo(15));

                window.Close();
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [AvaloniaTest]
        public async Task OffersUnitsNextToAnOpenedSprite()
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

                // The sprite is shown, with directions from the descriptor; the unit can be picked.
                Assert.That(window.CurrentUnit, Is.Null);
                Assert.That(window.Directions.Source, Is.EqualTo(GpDirectionsSource.Descriptor));
                Assert.That(window.FrameCount, Is.EqualTo(4), "4 poses of one direction");
                Assert.That(window.FindControl<StackPanel>("UnitPanel").IsVisible, Is.True);

                await window.SelectUnit("TST");
                Assert.That(window.CurrentUnit.Descriptor.FileName, Is.EqualTo("TST"));
                Assert.That(window.FindControl<ComboBox>("StateBox").ItemCount, Is.EqualTo(5));

                window.Close();
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [AvaloniaTest]
        public async Task SelectsAndWalksTheUnitOnTheField()
        {
            var folder = Path.Combine(Path.GetTempPath(), "viewer-field-" + Guid.NewGuid());
            Directory.CreateDirectory(folder);
            var archive = Path.Combine(folder, "All.gsc");
            File.WriteAllBytes(archive, UnitTests.GpTestFiles.GameArchive(UnitTests.GpTestFiles.UnitGameData()));

            try
            {
                var window = new MainWindow(archive);
                window.Show();
                await window.RenderingCompleted;
                await window.SetDirection(4);

                // On the field the unit stands, facing the way it faced, and the animation plays.
                await window.SetFieldMode(true);
                window.Pause(); // the test drives the ticks
                window.UpdateLayout();

                var field = window.FindControl<Canvas>("Field");
                Assert.That(field.IsVisible, Is.True);
                Assert.That(window.FindControl<ScrollViewer>("ImageScroller").IsVisible, Is.False);
                Assert.That(window.CurrentState.Code, Is.EqualTo("STAND"));
                Assert.That(window.CurrentDirection, Is.EqualTo(4), "west");
                var unit = window.FieldUnit;
                var start = unit.Position;
                Assert.That(DrawnColumnsAtFeet(window), Is.EqualTo(new[] { 0, 1, 2, 3, 4 }), "feet at the anchor, line to the right");

                // Right-click does nothing until the unit is selected; a left-click elsewhere does not select it.
                Click(window, start + new Vector(0, -100), MouseButton.Right);
                Assert.That(unit.IsWalking, Is.False);
                Click(window, start + new Vector(60, 60), MouseButton.Left);
                Assert.That(window.IsUnitSelected, Is.False);

                Click(window, start, MouseButton.Left);
                Assert.That(window.IsUnitSelected, Is.True);
                Assert.That(window.FindControl<Avalonia.Controls.Shapes.Ellipse>("SelectionMarker").IsVisible, Is.True);

                // Sent north: it turns and walks.
                Click(window, start + new Vector(0, -100), MouseButton.Right);
                await window.RenderingCompleted;
                Assert.That(unit.IsWalking, Is.True);
                Assert.That(window.CurrentState.Code, Is.EqualTo("MOTION_L+MOTION_R"));
                Assert.That(window.CurrentDirection, Is.EqualTo(8), "north");
                Assert.That(window.FrameCount, Is.EqualTo(4));

                for (var i = 0; i < 10; i++)
                {
                    window.Tick();
                }

                Assert.That(unit.Position, Is.EqualTo(start + new Vector(0, -10 * FieldUnit.DefaultStepLength)));
                Assert.That(window.CurrentState.Code, Is.EqualTo("MOTION_L+MOTION_R"));

                // On arrival it stands again, still facing north.
                for (var i = 0; i < 15; i++)
                {
                    window.Tick();
                }

                await window.RenderingCompleted;
                Assert.That(unit.IsWalking, Is.False);
                Assert.That(unit.Position, Is.EqualTo(start + new Vector(0, -100)));
                Assert.That(window.CurrentState.Code, Is.EqualTo("STAND"));
                Assert.That(window.CurrentDirection, Is.EqualTo(8));

                // Sent east: the mirrored frames are drawn with the feet at the unit's position too.
                var here = unit.Position;
                Click(window, here + new Vector(80, 0), MouseButton.Right);
                await window.RenderingCompleted;
                Assert.That(window.CurrentState.Code, Is.EqualTo("MOTION_L+MOTION_R"));
                Assert.That(window.CurrentDirection, Is.EqualTo(12), "east");
                window.Tick();
                await window.RenderingCompleted;
                Assert.That(DrawnColumnsAtFeet(window), Is.EqualTo(new[] { -5, -4, -3, -2, -1 }));

                // Turning while walking keeps the step it is in.
                window.Tick();
                var pose = window.CurrentFrame;
                Click(window, unit.Position + new Vector(-50, 50), MouseButton.Right);
                await window.RenderingCompleted;
                Assert.That(window.CurrentDirection, Is.EqualTo(2), "south-west");
                Assert.That(window.CurrentFrame, Is.EqualTo(pose));

                // Leaving the field goes back to the plain view.
                await window.SetFieldMode(false);
                Assert.That(window.FieldUnit, Is.Null);
                Assert.That(field.IsVisible, Is.False);
                Assert.That(window.CurrentState.Code, Is.EqualTo("MOTION_L+MOTION_R"));

                window.Close();
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        private static void Click(MainWindow window, Point fieldPoint, MouseButton button)
        {
            var point = window.FindControl<Canvas>("Field").TranslatePoint(fieldPoint, window).Value;
            window.MouseDown(point, button);
            window.MouseUp(point, button);
        }

        /// <summary>Columns of the unit's row (its feet) that are drawn on the field, relative to the feet.</summary>
        private static int[] DrawnColumnsAtFeet(MainWindow window)
        {
            var image = window.FindControl<Image>("FieldImage");
            var bitmap = (WriteableBitmap)image.Source;
            var feet = window.FieldUnit.Position;
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

        /// <summary>Columns of row 0 that differ from the gray background.</summary>
        private static int[] DrawnColumns(Image image)
        {
            var bitmap = (WriteableBitmap)image.Source;
            var width = bitmap.PixelSize.Width;
            var row = new byte[width * 4];

            using (var buffer = bitmap.Lock())
            {
                Marshal.Copy(buffer.Address, row, 0, row.Length);
            }

            return Enumerable.Range(0, width)
                .Where(x => row[4 * x] != 0x80 || row[4 * x + 1] != 0x80 || row[4 * x + 2] != 0x80)
                .ToArray();
        }

        private static string HashOf(WriteableBitmap bitmap)
        {
            var size = bitmap.PixelSize;
            var pixels = new byte[size.Width * size.Height * 4];

            using (var buffer = bitmap.Lock())
            {
                for (int y = 0; y < size.Height; y++)
                {
                    Marshal.Copy(buffer.Address + y * buffer.RowBytes, pixels, y * size.Width * 4, size.Width * 4);
                }
            }

            return size.Width + "x" + size.Height + ":" + Convert.ToHexString(SHA256.HashData(pixels));
        }
    }
}
