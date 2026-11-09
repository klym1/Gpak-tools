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
using Color = System.Drawing.Color;

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
                    log.AppendFormat("[{0,4}] - {1:g}", result.Number, result.Elapsed);
                    if (result.Error != null)
                    {
                        log.Append(" (").Append(result.Error).Append(')');
                    }

                    log.AppendLine();
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
            var file = GpFile.Load(gpFile);
            var renderer = new GpRenderer(Palette.Load(PaletteFile))
            {
                PlayerColor = NationColorOffset.Blue,
                Background = Color.FromArgb(255, 0x80, 0x80, 0x80),
            };

            return file.Frames
                .Select(frame =>
                {
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        var rendered = renderer.Render(file, frame);
                        var warning = rendered.SkippedParts.Count > 0
                            ? rendered.SkippedParts.Count + " unsupported part(s) skipped"
                            : null;
                        return new RenderResult(frame.Index, rendered.Image, warning, stopwatch.Elapsed);
                    }
                    catch (Exception ex) when (ex is InvalidDataException || ex is NotSupportedException)
                    {
                        return new RenderResult(frame.Index, null, ex.Message, stopwatch.Elapsed);
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
