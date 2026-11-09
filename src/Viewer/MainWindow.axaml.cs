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
        private int _current = -1;
        private GpDirections _directions;
        private bool _updatingControls;
        private int _renderVersion;
        private int _loadCount;
        private bool _loading;

        /// <summary>Completes when the last requested file or color has been rendered and shown.</summary>
        public Task RenderingCompleted { get; private set; } = Task.CompletedTask;

        /// <summary>The file currently shown, or null.</summary>
        public string CurrentFile { get; private set; }

        public bool IsPlaying => _timer.IsEnabled;

        /// <summary>Number of the frame (or, by direction, the pose) currently shown, or -1.</summary>
        public int CurrentFrame => _current >= 0 ? _images[_current].Number : -1;

        /// <summary>Number of frames (or, by direction, poses) that can be shown.</summary>
        public int FrameCount => _images.Count;

        /// <summary>Directions of the current file, or null if it has none.</summary>
        public GpDirections Directions => _directions;

        /// <summary>Whether the list shows the poses of one direction rather than all frames.</summary>
        public bool ShowsDirection => _directions != null && ByDirectionBox.IsChecked == true;

        /// <summary>The selected direction (0 = south, clockwise), or -1.</summary>
        public int CurrentDirection => ShowsDirection ? DirectionBox.SelectedIndex : -1;

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
            PlayerColorBox.SelectionChanged += (sender, args) => Rerender(resetRange: false);
            DirectionBox.SelectionChanged += (sender, args) => Rerender(resetRange: false);
            ByDirectionBox.IsCheckedChanged += (sender, args) =>
            {
                DirectionBox.IsEnabled = ByDirectionBox.IsChecked == true;
                Rerender(resetRange: true);
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

        /// <summary>Shows another direction, keeping pose, loop range and playback.</summary>
        public Task SetDirection(int direction)
        {
            ByDirectionBox.IsChecked = true;
            DirectionBox.SelectedIndex = direction;
            return RenderingCompleted;
        }

        /// <summary>Switches between the poses of one direction and all frames in file order.</summary>
        public Task SetShowByDirection(bool byDirection)
        {
            ByDirectionBox.IsChecked = byDirection;
            return RenderingCompleted;
        }

        /// <summary>Shows the image at a position (0 to <see cref="FrameCount"/> - 1), or nothing for -1.</summary>
        public void ShowFrameAt(int index)
        {
            _current = index >= 0 && index < _images.Count ? index : -1;
            ImageView.Source = _current >= 0 ? _images[_current].Bitmap : null;
            FrameText.Text = _current >= 0 ? (ShowsDirection ? "Pose " : "Frame ") + _images[_current].Number + " / " + _images[^1].Number : "";
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

            var position = loop.IndexOf(_current);
            position = position < 0
                ? (direction >= 0 ? 0 : loop.Count - 1)
                : ((position + direction) % loop.Count + loop.Count) % loop.Count;

            ShowFrameAt(loop[position]);
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
                GpDirections directions;
                try
                {
                    (file, directions) = await Task.Run(() =>
                    {
                        var loaded = GpFile.Load(gpFile);
                        return (loaded, GpDirections.Detect(gpFile, loaded));
                    });
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
                    _file = file;
                    CurrentFile = gpFile;
                    ShowDirections(directions);
                    await RenderAsync(resetRange: true);
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

        /// <summary>Re-renders the current file after a change of color, direction or mode.</summary>
        private void Rerender(bool resetRange)
        {
            // While a file loads, it picks up the change when it renders.
            if (_file != null && !_loading && !_updatingControls)
            {
                RenderingCompleted = RenderAsync(resetRange);
            }
        }

        /// <summary>Fills the direction picker; keeps the chosen direction if the new file has the same directions.</summary>
        private void ShowDirections(GpDirections directions)
        {
            var previous = _directions != null && directions != null && _directions.Count == directions.Count
                ? DirectionBox.SelectedIndex
                : 0;

            _updatingControls = true;
            try
            {
                _directions = directions;
                DirectionPanel.IsVisible = directions != null;
                DirectionBox.ItemsSource = directions == null
                    ? Array.Empty<DirectionOption>()
                    : Enumerable.Range(0, directions.Count).Select(i => new DirectionOption(i, LabelOf(directions, i))).ToArray();
                DirectionBox.SelectedIndex = directions == null ? -1 : Math.Max(previous, 0);

                var source = directions == null
                    ? null
                    : directions.Count + " directions, " + directions.Poses + " poses each"
                      + (directions.Source == GpDirectionsSource.Descriptor
                          ? ", from " + directions.DescriptorName + ".MD"
                          : ", recognized from the frames")
                      + (directions.IsMirrored ? "; the eastern half is mirrored" : "");
                ToolTip.SetTip(DirectionBox, source);
            }
            finally
            {
                _updatingControls = false;
            }
        }

        private static string LabelOf(GpDirections directions, int direction)
        {
            var name = directions.NameOf(direction);
            var arrow = name switch
            {
                "S" => "↓ ",
                "SW" => "↙ ",
                "W" => "← ",
                "NW" => "↖ ",
                "N" => "↑ ",
                "NE" => "↗ ",
                "E" => "→ ",
                "SE" => "↘ ",
                _ => "   ",
            };
            return arrow + name;
        }

        /// <summary>
        /// Renders the frames to show off the UI thread: the poses of the selected direction, or all frames.
        /// With <paramref name="resetRange"/>, the loop range covers everything and the first frame is shown;
        /// otherwise the shown frame, loop range and playback stay as they are.
        /// </summary>
        private async Task RenderAsync(bool resetRange)
        {
            var version = ++_renderVersion;
            var file = _file;
            var color = PlayerColor;
            var directions = ShowsDirection ? _directions : null;
            var direction = Math.Max(DirectionBox.SelectedIndex, 0);

            var results = await Task.Run(() => Render(file, _palette, color, directions, direction));

            // A newer file, color or direction was requested while this one was rendering.
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
            _images = images;

            Title = "GP Viewer - " + Path.GetFileName(CurrentFile);
            FileNameText.Text = Path.GetFileName(CurrentFile) + (_directions == null
                ? " (" + file.Frames.Count + " frames)"
                : " (" + _directions.Poses + " poses × " + _directions.Count + " directions)");

            if (!resetRange)
            {
                var index = images.FindIndex(image => image.Number == shownFrame);
                ShowFrameAt(index >= 0 ? index : 0);
                return;
            }

            var last = Math.Max(results.Length - 1, 0);
            FromFrame.Maximum = last;
            ToFrame.Maximum = last;
            FromFrame.Value = 0;
            ToFrame.Value = last;

            ShowFrameAt(0);
        }

        /// <summary>
        /// Renders all frames, or with <paramref name="directions"/> the poses of one direction.
        /// All images share one canvas size (and for directions, one anchor at its centre), so the
        /// animation does not jump when playing or changing direction.
        /// </summary>
        private static RenderResult[] Render(GpFile file, Palette palette, NationColorOffset color, GpDirections directions, int direction)
        {
            var renderer = new GpRenderer(palette)
            {
                PlayerColor = color,
                Background = Color.FromArgb(255, 0x80, 0x80, 0x80),
            };

            var right = file.Frames.Select(frame => frame.Width).DefaultIfEmpty(1).Max();
            var height = file.Frames.Select(frame => frame.Height).DefaultIfEmpty(1).Max();

            RenderResult RenderOne(int number, GpFrame frame, int width, int offsetX, bool mirror)
            {
                try
                {
                    var rendered = renderer.Render(file, frame, width, height, offsetX, mirror);
                    return new RenderResult(number, rendered.Image, null, rendered.SkippedParts.Count);
                }
                catch (Exception ex) when (ex is InvalidDataException || ex is NotSupportedException)
                {
                    return new RenderResult(number, null, ex.Message, 0);
                }
            }

            if (directions == null)
            {
                return file.Frames.Select(frame => RenderOne(frame.Index, frame, right, 0, false)).ToArray();
            }

            // Centre the canvas on the anchor so that mirrored directions stay in place.
            var left = file.Frames.SelectMany(frame => frame.Parts).Select(part => part.X).DefaultIfEmpty(0).Min();
            var half = Math.Max(Math.Max(directions.AnchorX - Math.Min(left, 0), right - directions.AnchorX), 1);
            var offset = half - directions.AnchorX;

            return Enumerable.Range(0, directions.Poses)
                .Select(pose =>
                {
                    var (frame, mirrored) = directions.FrameOf(pose, direction);
                    return RenderOne(pose, file.Frames[frame], 2 * half, offset, mirrored);
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

    public sealed record DirectionOption(int Index, string Label);
}
