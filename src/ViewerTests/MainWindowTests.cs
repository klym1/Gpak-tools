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
using Avalonia.Media.Imaging;
using NUnit.Framework;
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

            var list = window.FindControl<ListBox>("ImageList");
            var image = window.FindControl<Image>("ImageView");
            var log = window.FindControl<TextBox>("LogBox");

            Assert.That(window.Title, Is.EqualTo("GP Viewer - test.gp"));
            Assert.That(list.ItemCount, Is.EqualTo(15));
            Assert.That(list.SelectedIndex, Is.EqualTo(0));
            Assert.That(log.Text, Does.Contain("Finished in").And.Not.Contain("failed"));

            var bitmap = (WriteableBitmap)image.Source;
            Assert.That(HashOf(bitmap), Is.EqualTo(ExpectedFirstImage));

            // Selecting another entry shows that image.
            list.SelectedIndex = 9;
            Assert.That(((Bitmap)image.Source).PixelSize, Is.EqualTo(new PixelSize(453, 435)));

            // Keep a screenshot of the window as a test artifact.
            list.SelectedIndex = 1;
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

            Assert.That(window.FindControl<TextBox>("LogBox").Text, Does.StartWith("Failed to open"));
            Assert.That(window.FindControl<ListBox>("ImageList").ItemCount, Is.EqualTo(0));

            window.Close();
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
