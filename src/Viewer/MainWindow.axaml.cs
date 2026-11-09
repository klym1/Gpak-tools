using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
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

        private static readonly FilePickerFileType GpFileType = new FilePickerFileType("GP sprites")
        {
            Patterns = new[] { "*.gp", "*.GP" },
        };

        private readonly DispatcherTimer _timer = new DispatcherTimer();
        private readonly Palette _palette = Palette.Load(PaletteFile);
        private IReadOnlyList<RenderedImage> _images = Array.Empty<RenderedImage>();
        private GpFile _file;
        private int _renderVersion;
        private int _loadCount;
        private bool _loading;

        /// <summary>Completes when the last requested file or color has been rendered and shown.</summary>
        public Task RenderingCompleted { get; private set; } = Task.CompletedTask;

        /// <summary>The file currently shown, or null.</summary>
        public string CurrentFile { get; private set; }

        public bool IsPlaying => _timer.IsEnabled;

        /// <summary>Number of the frame currently shown, or -1.</summary>
        public int CurrentFrame => (ImageList.SelectedItem as RenderedImage)?.Number ?? -1;

        /// <summary>Player color the frames are rendered with.</summary>
        public NationColorOffset PlayerColor => (PlayerColorBox.SelectedItem as PlayerColorOption)?.Offset ?? NationColorOffset.Blue;

        public MainWindow() : this(DefaultGpFile)
        {
        }

        public MainWindow(string gpFile)
        {
            InitializeComponent();

            var colorOptions = NationColorOffset.All
                .Select(color => new PlayerColorOption(color.Name, color.Offset, SwatchOf(color.Offset)))
                .ToList();
            PlayerColorBox.ItemsSource = colorOptions;
            PlayerColorBox.SelectedItem = colorOptions.First(option => option.Offset.Offset == NationColorOffset.Blue.Offset);
            PlayerColorBox.SelectionChanged += (sender, args) =>
            {
                // A file that is still loading picks up the new color when it renders.
                if (_file != null && !_loading)
                {
                    RenderingCompleted = RenderAsync(_file, CurrentFile, keepPosition: true);
                }
            };

            ImageList.SelectionChanged += (sender, args) =>
            {
                ImageView.Source = (ImageList.SelectedItem as RenderedImage)?.Bitmap;
            };

            OpenButton.Click += async (sender, args) => await PickFileAsync();
            PlayButton.Click += (sender, args) => TogglePlayback();
            PreviousButton.Click += (sender, args) => StepFrame(-1);
            NextButton.Click += (sender, args) => StepFrame(1);
            FramesPerSecond.ValueChanged += (sender, args) => UpdateTimerInterval();

            AddHandler(DragDrop.DragOverEvent, OnDragOver);
            AddHandler(DragDrop.DropEvent, OnDrop);

            _timer.Tick += (sender, args) => StepFrame(1);
            UpdateTimerInterval();

            Opened += (sender, args) => LoadFile(gpFile);
            Closed += (sender, args) => _timer.Stop();
        }

        /// <summary>Loads and renders a file; <see cref="RenderingCompleted"/> completes when it is shown.</summary>
        public Task LoadFile(string gpFile)
        {
            RenderingCompleted = LoadAsync(gpFile);
            return RenderingCompleted;
        }

        /// <summary>Re-renders the current file in another player color, keeping frame, loop range and playback.</summary>
        public Task SetPlayerColor(NationColorOffset color)
        {
            PlayerColorBox.SelectedItem = PlayerColorBox.Items
                .OfType<PlayerColorOption>()
                .First(option => option.Offset.Offset == color.Offset);
            return RenderingCompleted;
        }

        public void Play()
        {
            if (_images.Count == 0)
            {
                return;
            }

            _timer.Start();
            PlayButton.Content = "⏸ Pause";
        }

        public void Pause()
        {
            _timer.Stop();
            PlayButton.Content = "▶ Play";
        }

        public void TogglePlayback()
        {
            if (IsPlaying)
            {
                Pause();
            }
            else
            {
                Play();
            }
        }

        /// <summary>
        /// Moves forward or backward through the frames between From and To, wrapping around at the ends.
        /// </summary>
        public void StepFrame(int direction)
        {
            var from = (int) (FromFrame.Value ?? 0);
            var to = (int) (ToFrame.Value ?? int.MaxValue);
            if (from > to)
            {
                (from, to) = (to, from);
            }

            var loop = Enumerable.Range(0, _images.Count)
                .Where(i => _images[i].Number >= from && _images[i].Number <= to)
                .ToList();

            if (loop.Count == 0)
            {
                return;
            }

            var position = loop.IndexOf(ImageList.SelectedIndex);
            position = position < 0
                ? (direction >= 0 ? 0 : loop.Count - 1)
                : ((position + direction) % loop.Count + loop.Count) % loop.Count;

            ImageList.SelectedIndex = loop[position];
            ImageList.ScrollIntoView(loop[position]);
        }

        private IBrush SwatchOf(NationColorOffset color)
        {
            // The second-brightest entry of the ramp reads best as a swatch.
            var c = _palette[color.Offset + 1];
            return new SolidColorBrush(Avalonia.Media.Color.FromRgb(c.R, c.G, c.B));
        }

        private void UpdateTimerInterval()
        {
            var fps = (double) (FramesPerSecond.Value ?? 12);
            _timer.Interval = TimeSpan.FromSeconds(1 / Math.Clamp(fps, 1, 60));
        }

        private async Task PickFileAsync()
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open GP file",
                AllowMultiple = false,
                FileTypeFilter = new[] { GpFileType, FilePickerFileTypes.All },
            });

            var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (path != null)
            {
                await LoadFile(path);
            }
        }

        private static string FirstDroppedFile(DragEventArgs e)
        {
            return e.DataTransfer.TryGetFiles()?
                .Select(item => item.TryGetLocalPath())
                .FirstOrDefault(path => path != null);
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            e.DragEffects = FirstDroppedFile(e) != null ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private async void OnDrop(object sender, DragEventArgs e)
        {
            var path = FirstDroppedFile(e);
            if (path != null)
            {
                await LoadFile(path);
            }
        }

        private async Task LoadAsync(string gpFile)
        {
            var version = ++_renderVersion;
            Pause();
            StatusText.Text = "Loading " + gpFile + "…";
            var load = ++_loadCount;
            _loading = true;

            try
            {
                GpFile file;
                try
                {
                    file = await Task.Run(() => GpFile.Load(gpFile));
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    if (version == _renderVersion)
                    {
                        StatusText.Text = "Failed to open " + gpFile + ": " + ex.Message;
                    }

                    return;
                }

                if (version == _renderVersion)
                {
                    await RenderAsync(file, gpFile, keepPosition: false);
                }
            }
            finally
            {
                // Only the most recent load clears the flag.
                if (load == _loadCount)
                {
                    _loading = false;
                }
            }
        }

        /// <summary>
        /// Renders all frames off the UI thread and shows them. With <paramref name="keepPosition"/>,
        /// the shown frame, loop range and playback stay as they are (used when the color changes).
        /// </summary>
        private async Task RenderAsync(GpFile file, string gpFile, bool keepPosition)
        {
            var version = ++_renderVersion;
            var color = PlayerColor;

            var results = await Task.Run(() => Render(file, _palette, color));

            // A newer file or color was requested while this one was rendering.
            if (version != _renderVersion)
            {
                return;
            }

            var images = results
                .Where(result => result.Image != null)
                .Select(result => new RenderedImage(result.Number, result.Image.ToWriteableBitmap()))
                .ToList();

            StatusText.Text = StatusOf(results);

            var shownFrame = CurrentFrame;
            _file = file;
            _images = images;
            ImageList.ItemsSource = images;

            if (keepPosition)
            {
                var index = images.ToList().FindIndex(image => image.Number == shownFrame);
                ImageList.SelectedIndex = index >= 0 ? index : (images.Count > 0 ? 0 : -1);
                return;
            }

            CurrentFile = gpFile;
            Title = "GP Viewer - " + Path.GetFileName(gpFile);
            FileNameText.Text = Path.GetFileName(gpFile) + " (" + results.Length + " frames)";

            var lastFrame = Math.Max(results.Length - 1, 0);
            FromFrame.Maximum = lastFrame;
            ToFrame.Maximum = lastFrame;
            FromFrame.Value = 0;
            ToFrame.Value = lastFrame;

            ImageList.SelectedIndex = images.Count > 0 ? 0 : -1;
        }

        private static RenderResult[] Render(GpFile file, Palette palette, NationColorOffset color)
        {
            var renderer = new GpRenderer(palette)
            {
                PlayerColor = color,
                Background = Color.FromArgb(255, 0x80, 0x80, 0x80),
            };

            // One canvas size for all frames, so the animation does not jump.
            var width = file.Frames.Select(frame => frame.Width).DefaultIfEmpty(1).Max();
            var height = file.Frames.Select(frame => frame.Height).DefaultIfEmpty(1).Max();

            return file.Frames
                .Select(frame =>
                {
                    try
                    {
                        var rendered = renderer.Render(file, frame, width, height);
                        return new RenderResult(frame.Index, rendered.Image, null, rendered.SkippedParts.Count);
                    }
                    catch (Exception ex) when (ex is InvalidDataException || ex is NotSupportedException)
                    {
                        return new RenderResult(frame.Index, null, ex.Message, 0);
                    }
                })
                .ToArray();
        }

        /// <summary>Describes frames that failed or are incomplete; empty when everything rendered.</summary>
        private static string StatusOf(IReadOnlyList<RenderResult> results)
        {
            var messages = new List<string>();

            var failed = results.Where(result => result.Image == null).ToList();
            if (failed.Count > 0)
            {
                messages.Add(failed.Count + " frame(s) could not be decoded, e.g. frame " + failed[0].Number + ": " + failed[0].Error);
            }

            var incomplete = results.Count(result => result.SkippedParts > 0);
            if (incomplete > 0)
            {
                messages.Add(incomplete + " frame(s) contain parts in an unsupported encoding, which are not shown.");
            }

            return string.Join(" ", messages);
        }

        private sealed record RenderResult(int Number, ImageView Image, string Error, int SkippedParts);
    }

    public sealed record RenderedImage(int Number, Bitmap Bitmap)
    {
        public override string ToString() => Number.ToString();
    }

    public sealed record PlayerColorOption(string Name, NationColorOffset Offset, IBrush Swatch);
}
