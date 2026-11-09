using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using GPExtractor;
using ImageRenderer;
using Types;

namespace Viewer
{
    public partial class MainWindow : Window
    {
        public static string DefaultGpFile => Path.Combine(AppContext.BaseDirectory, "gp", "test.gp");

        private static string PaletteFile => Path.Combine(AppContext.BaseDirectory, "palette", "0", "agew_1.pal");

        /// <summary>Completes when all images of the file have been rendered and shown.</summary>
        public Task RenderingCompleted { get; private set; } = Task.CompletedTask;

        public MainWindow() : this(DefaultGpFile)
        {
        }

        public MainWindow(string gpFile)
        {
            InitializeComponent();

            Title = "GP Viewer - " + Path.GetFileName(gpFile);

            ImageList.SelectionChanged += (sender, args) =>
            {
                ImageView.Source = (ImageList.SelectedItem as RenderedImage)?.Bitmap;
            };

            Opened += (sender, args) => RenderingCompleted = LoadAsync(gpFile);
        }

        private async Task LoadAsync(string gpFile)
        {
            var log = new StringBuilder();

            RenderResult[] results;
            try
            {
                // Parse and render off the UI thread so the window stays responsive.
                results = await Task.Run(() => Render(gpFile));
            }
            catch (Exception ex)
            {
                LogBox.Text = "Failed to open " + gpFile + Environment.NewLine + ex.Message;
                return;
            }

            var images = new List<RenderedImage>();

            foreach (var result in results)
            {
                if (result.Image != null)
                {
                    images.Add(new RenderedImage(result.Number, result.Image.ToWriteableBitmap()));
                    log.AppendFormat("[{0,4}] - {1:g}", result.Number, result.Elapsed).AppendLine();
                }
                else
                {
                    log.AppendFormat("[{0,4}] - failed: {1}", result.Number, result.Error).AppendLine();
                }
            }

            log.AppendFormat("Finished in {0:c}", results.Aggregate(TimeSpan.Zero, (sum, r) => sum + r.Elapsed)).AppendLine();

            LogBox.Text = log.ToString();
            ImageList.ItemsSource = images;
            ImageList.SelectedIndex = images.Count > 0 ? 0 : -1;
        }

        private static RenderResult[] Render(string gpFile)
        {
            var extractor = new Extractor();
            var imagePaletteBytes = extractor.GetPaletteBytes(gpFile);
            var layouts = extractor.GetImagesFromOutput(gpFile).ToList();

            var rawParser = new RawParser();
            var colorCollection = rawParser.GetColorCollectionFromPalleteFile(File.ReadAllBytes(PaletteFile));
            var imagePaletteColors = ImageGenerator.OffsetsToColors(imagePaletteBytes, colorCollection);

            var nationColorOffset = NationColorOffset.Blue;

            return Enumerable.Range(0, layouts.Count)
                .Select(number =>
                {
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        var image = new Runner().Run(layouts, nationColorOffset, number, rawParser, new BitmapRenderer(), imagePaletteColors, colorCollection);
                        return new RenderResult(number, image, null, stopwatch.Elapsed);
                    }
                    catch (Exception ex)
                    {
                        return new RenderResult(number, null, ex.Message, stopwatch.Elapsed);
                    }
                })
                .ToArray();
        }

        private sealed record RenderResult(int Number, ImageView Image, string Error, TimeSpan Elapsed);
    }

    public sealed record RenderedImage(int Number, Bitmap Bitmap)
    {
        public override string ToString() => Number.ToString();
    }
}
