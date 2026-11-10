using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
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
    /// <summary>
    /// A field to put the game's units on: open the game data, add units, select them and send them around
    /// (see MainWindow.Field.cs).
    /// </summary>
    public partial class MainWindow : Window
    {
        private static string PaletteFile => Path.Combine(AppContext.BaseDirectory, "palette", "0", "agew_1.pal");

        private static readonly FilePickerFileType GameFileType = new FilePickerFileType("Game archives and sprites")
        {
            Patterns = new[] { "*.gsc", "*.GSC", "*.gp", "*.GP" },
        };

        private readonly DispatcherTimer _timer = new DispatcherTimer();
        private readonly Palette _palette = Palette.Load(PaletteFile);

        private GameCatalog _catalog;
        private bool _updatingControls;
        private int _loadVersion;

        /// <summary>Completes when the last requested game data and unit frames have been loaded and drawn.</summary>
        public Task RenderingCompleted { get; private set; } = Task.CompletedTask;

        /// <summary>The archive, folder or sprite opened last, or null.</summary>
        public string CurrentFile { get; private set; }

        /// <summary>The game data whose units are on the field, or null.</summary>
        public GameCatalog Catalog => _catalog;

        public bool IsPlaying => _timer.IsEnabled;

        /// <summary>Player color of the selected units and of units added next.</summary>
        public NationColorOffset PlayerColor => (PlayerColorBox.SelectedItem as PlayerColorOption)?.Offset ?? NationColorOffset.Blue;

        public MainWindow() : this(null)
        {
        }

        /// <param name="path">The game's All.gsc, a folder extracted from it, or a sprite inside one; or null.</param>
        public MainWindow(string path)
        {
            InitializeComponent();

            var colorOptions = NationColorOffset.All
                .Select(color => new PlayerColorOption(color.Name, color.Offset, SwatchOf(color.Offset)))
                .ToList();
            PlayerColorBox.ItemsSource = colorOptions;
            PlayerColorBox.SelectedItem = colorOptions.First(option => option.Offset.Offset == NationColorOffset.Blue.Offset);

            OpenButton.Click += async (sender, args) => await PickFileAsync();
            PlayButton.Click += (sender, args) => TogglePlayback();
            FramesPerSecond.ValueChanged += (sender, args) => UpdateTimerInterval();
            InitializeField();

            AddHandler(DragDrop.DragOverEvent, OnDragOver);
            AddHandler(DragDrop.DropEvent, OnDrop);

            _timer.Tick += (sender, args) => Tick();
            UpdateTimerInterval();

            Opened += (sender, args) =>
            {
                if (path != null)
                {
                    LoadFile(path);
                }
                else
                {
                    UpdateFieldView();
                }
            };
            Closed += (sender, args) =>
            {
                _timer.Stop();
                _catalog?.Dispose();
            };
        }

        /// <summary>
        /// Opens game data: the game's All.gsc, a folder extracted from it, or a sprite inside such a folder.
        /// The field starts over with the first unit; <see cref="RenderingCompleted"/> completes when it is drawn.
        /// </summary>
        public Task LoadFile(string path)
        {
            RenderingCompleted = LoadAsync(path);
            return RenderingCompleted;
        }

        /// <summary>Recolors the selected units and colors the ones added next.</summary>
        public Task SetPlayerColor(NationColorOffset color)
        {
            PlayerColorBox.SelectedItem = PlayerColorBox.Items
                .OfType<PlayerColorOption>()
                .First(option => option.Offset.Offset == color.Offset);
            return RenderingCompleted;
        }

        public void Play()
        {
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

        private IBrush SwatchOf(NationColorOffset color)
        {
            // The second-brightest entry of the ramp reads best as a swatch.
            var c = _palette[color.Offset + 1];
            return new SolidColorBrush(Avalonia.Media.Color.FromRgb(c.R, c.G, c.B));
        }

        private void UpdateTimerInterval()
        {
            var fps = (double) (FramesPerSecond.Value ?? 25);
            _timer.Interval = TimeSpan.FromSeconds(1 / Math.Clamp(fps, 1, 60));
        }

        private async Task PickFileAsync()
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open the game's All.gsc",
                AllowMultiple = false,
                FileTypeFilter = new[] { GameFileType, FilePickerFileTypes.All },
            });

            var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (path != null)
            {
                await LoadFile(path);
            }
        }

        private static string FirstDroppedPath(DragEventArgs e)
        {
            return e.DataTransfer.TryGetFiles()?
                .Select(item => item.TryGetLocalPath())
                .FirstOrDefault(path => path != null);
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            e.DragEffects = FirstDroppedPath(e) != null ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private async void OnDrop(object sender, DragEventArgs e)
        {
            var path = FirstDroppedPath(e);
            if (path != null)
            {
                await LoadFile(path);
            }
        }

        /// <summary>
        /// Loads the game data at a path: an archive or folder, or the folder of descriptors around a sprite.
        /// Returns null if a sprite has no game data around it.
        /// </summary>
        private static GameCatalog OpenGameData(string path)
        {
            if (Directory.Exists(path) || string.Equals(Path.GetExtension(path), ".gsc", StringComparison.OrdinalIgnoreCase))
            {
                return GameCatalog.Load(GameFiles.Open(path));
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException("The file does not exist.", path);
            }

            var folder = DescriptorFolderOf(path);
            return folder == null ? null : GameCatalog.Load(new FolderGameFiles(folder));
        }

        /// <summary>The folder with game descriptors (*.MD) for a sprite: its own folder or the parent, or null.</summary>
        private static string DescriptorFolderOf(string spritePath)
        {
            var folder = Path.GetDirectoryName(Path.GetFullPath(spritePath));
            foreach (var candidate in new[] { folder, Path.GetDirectoryName(folder) })
            {
                try
                {
                    if (candidate != null && Directory.EnumerateFiles(candidate).Any(IsGameDescriptor))
                    {
                        return candidate;
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                }
            }

            return null;
        }

        /// <summary>A game descriptor, not just any Markdown file: an .md file that names sprites.</summary>
        private static bool IsGameDescriptor(string file)
        {
            if (!string.Equals(Path.GetExtension(file), ".md", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                using var stream = File.OpenRead(file);
                var head = new byte[4096];
                var read = stream.Read(head, 0, head.Length);
                return System.Text.Encoding.Latin1.GetString(head, 0, read).Contains("USERLC", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return false;
            }
        }

        private async Task LoadAsync(string path)
        {
            var version = ++_loadVersion;
            StatusText.Text = "Loading " + path + "…";

            try
            {
                var (catalog, first) = await Task.Run(() =>
                {
                    var opened = OpenGameData(path);
                    // Start with a unit that can be drawn: some use sprites that are missing or not GP files.
                    var drawable = opened?.Units.FirstOrDefault(unit => HasSprites(opened, unit, StandStateOf(unit)));
                    return (opened, drawable ?? opened?.Units.FirstOrDefault());
                });
                if (version != _loadVersion)
                {
                    catalog?.Dispose();
                    return;
                }

                if (catalog == null)
                {
                    StatusText.Text = "No game data (unit descriptors) found next to " + path + ". Open the game's All.gsc or a folder extracted from it.";
                    return;
                }

                SetCatalog(catalog);
                CurrentFile = path;
                ViewerSettings.LastGameData = Path.GetFullPath(path);
                Title = "GP Viewer - " + Path.GetFileName(catalog.Files.Source);
                FileNameText.Text = Path.GetFileName(catalog.Files.Source);

                if (first == null)
                {
                    StatusText.Text = "No units found in " + path;
                    return;
                }

                StatusText.Text = "";
                AddToField(first);
                Play();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException)
            {
                if (version == _loadVersion)
                {
                    StatusText.Text = "Failed to open " + path + ": " + ex.Message;
                }
            }
        }

        /// <summary>Whether every sprite a state uses is there.</summary>
        private static bool HasSprites(GameCatalog catalog, GameObject unit, GameObjectState state)
        {
            return state.Frames
                .Select(frame => frame.Sprite)
                .Distinct()
                .All(index => unit.Sprite(index) is { } sprite && UnitSprite.Load(catalog, sprite.FileName) != null);
        }

        private void SetCatalog(GameCatalog catalog)
        {
            if (ReferenceEquals(catalog, _catalog))
            {
                return;
            }

            // Field units belong to the game data they come from.
            ClearField();
            _catalog?.Dispose();
            _catalog = catalog;

            _updatingControls = true;
            try
            {
                var units = catalog?.Units.ToList() ?? new List<GameObject>();
                UnitBox.ItemsSource = units;
                UnitBox.SelectedItem = null;
                UnitPanel.IsVisible = units.Count > 0;
                ToolTip.SetTip(UnitBox, catalog == null
                    ? null
                    : "Pick one of the " + units.Count + " units in " + catalog.Files.Source + " to put it on the field (type to search)");
            }
            finally
            {
                _updatingControls = false;
            }

            UpdateFieldView();
        }

        /// <summary>What to render: a unit's state from one direction, in one color.</summary>
        private sealed record RenderRequest(
            GameCatalog Catalog, GameObject Unit, GameObjectState State,
            GpDirections Directions, int Direction, NationColorOffset Color);

        /// <summary>One image to render: a frame of a sprite placed on the shared canvas.</summary>
        private sealed record FrameJob(int Number, UnitSprite File, int Frame, bool Mirror, int OffsetX, int OffsetY, string Error = null);

        /// <summary>
        /// Renders the frames on a transparent background; also returns where the anchor (the unit's feet) is
        /// in them.
        /// </summary>
        private static (RenderResult[] Results, PixelPoint Anchor) Render(RenderRequest request, Palette palette)
        {
            var renderer = new GpRenderer(palette)
            {
                PlayerColor = request.Color,
                Background = Color.Transparent,
            };

            var (jobs, width, height, anchor) = PlanUnit(request);

            var results = jobs.Select(job =>
                {
                    if (job.Error != null)
                    {
                        return new RenderResult(job.Number, null, job.Error, 0);
                    }

                    try
                    {
                        var rendered = job.File.Render(
                              renderer
                            , job.Frame
                            , width
                            , height
                            , job.OffsetX
                            , job.Mirror
                            , job.OffsetY
                        );
                        return new RenderResult(job.Number, rendered.Image, null, rendered.SkippedParts.Count, OpaqueBounds(rendered.Image));
                    }
                    catch (Exception ex) when (ex is InvalidDataException || ex is NotSupportedException)
                    {
                        return new RenderResult(job.Number, null, ex.Message, 0);
                    }
                })
                .ToArray();

            return (results, anchor);
        }

        /// <summary>The smallest rectangle around the drawn (not fully transparent) pixels, or null if there are none.</summary>
        private static PixelRect? OpaqueBounds(ImageView image)
        {
            int left = image.Width, top = image.Height, right = -1, bottom = -1;
            for (var y = 0; y < image.Height; y++)
            {
                for (var x = 0; x < image.Width; x++)
                {
                    if (image.Pixels[y * image.Width + x].A == 0)
                    {
                        continue;
                    }

                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }
            }

            return right < 0 ? null : new PixelRect(left, top, right - left + 1, bottom - top + 1);
        }

        /// <summary>
        /// The frames of a unit's state from one direction. Every sprite of the unit is placed with its anchor
        /// at the same point of one canvas, so the unit stays in place across states and directions.
        /// </summary>
        private static (IReadOnlyList<FrameJob> Jobs, int Width, int Height, PixelPoint Anchor) PlanUnit(RenderRequest request)
        {
            var unit = request.Unit;
            var catalog = request.Catalog;

            // Directions each sprite is stored with, for estimating anchors the descriptor does not give.
            var spriteDirections = unit.States
                .SelectMany(state => state.Frames.Select(frame => (frame.Sprite, state.Directions)))
                .GroupBy(pair => pair.Sprite)
                .ToDictionary(group => group.Key, group => group.Max(pair => pair.Directions));

            var sprites = new Dictionary<int, (UnitSprite File, int AnchorX, int AnchorY)>();
            foreach (var (index, directions) in spriteDirections)
            {
                var descriptor = unit.Sprite(index);
                var file = descriptor == null ? null : UnitSprite.Load(catalog, descriptor.FileName);
                if (file == null || file.FrameCount == 0)
                {
                    continue;
                }

                sprites[index] = (file,
                    descriptor.AnchorX ?? file.EstimateAnchorX(Math.Min(directions, file.FrameCount)),
                    descriptor.AnchorY ?? 0);
            }

            // Canvas: symmetric around the anchor horizontally (for mirroring), tall enough for every sprite.
            int half = 1, top = 0, bottom = 1;
            foreach (var (file, anchorX, anchorY) in sprites.Values)
            {
                foreach (var part in file.Bounds)
                {
                    half = Math.Max(half, Math.Max(anchorX - part.X, part.X + part.Width - anchorX));
                    top = Math.Min(top, part.Y - anchorY);
                    bottom = Math.Max(bottom, part.Y + part.Height - anchorY);
                }
            }

            var state = request.State;
            var stateDirections = request.Directions;

            var jobs = state.Frames
                .Select((frame, number) =>
                {
                    var name = unit.Sprite(frame.Sprite)?.FileName ?? "#" + frame.Sprite;
                    if (!sprites.TryGetValue(frame.Sprite, out var sprite))
                    {
                        return new FrameJob(number, null, 0, false, 0, 0, "sprite " + name + " is missing");
                    }

                    var stored = 0;
                    var mirrored = false;
                    if (stateDirections != null)
                    {
                        mirrored = request.Direction >= stateDirections.StoredDirections;
                        stored = mirrored ? stateDirections.Count - request.Direction : request.Direction;
                    }

                    var index = frame.Pose * state.Directions + stored;
                    if (index >= sprite.File.FrameCount)
                    {
                        return new FrameJob(number, null, 0, false, 0, 0, "pose " + frame.Pose + " is not in sprite " + name);
                    }

                    return new FrameJob(number, sprite.File, index, mirrored, half - sprite.AnchorX, -top - sprite.AnchorY);
                })
                .ToList();

            return (jobs, 2 * half, bottom - top, new PixelPoint(half, -top));
        }

        /// <summary>Describes frames that failed or are incomplete; empty when everything rendered.</summary>
        private static string StatusOf(IReadOnlyList<RenderResult> results)
        {
            var messages = new List<string>();

            var failed = results.Where(result => result.Image == null).ToList();
            if (failed.Count > 0)
            {
                messages.Add(failed.Count + " frame(s) could not be shown, e.g. frame " + failed[0].Number + ": " + failed[0].Error + ".");
            }

            var incomplete = results.Count(result => result.SkippedParts > 0);
            if (incomplete > 0)
            {
                messages.Add(incomplete + " frame(s) contain parts in an unsupported encoding, which are not shown.");
            }

            return string.Join(" ", messages);
        }

        private sealed record RenderResult(int Number, ImageView Image, string Error, int SkippedParts, PixelRect? Opaque = null);
    }

    /// <param name="Opaque">Where the drawn pixels are.</param>
    public sealed record RenderedImage(int Number, Bitmap Bitmap, PixelRect? Opaque = null)
    {
        public override string ToString() => Number.ToString();
    }

    public sealed record PlayerColorOption(string Name, NationColorOffset Offset, IBrush Swatch);
}
