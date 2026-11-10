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
    /// Shows either a sprite file (all frames, or the poses of one direction) or a unit from the game data
    /// (one state, seen from one direction).
    /// </summary>
    public partial class MainWindow : Window
    {
        public static string DefaultGpFile => Path.Combine(AppContext.BaseDirectory, "gp", "test.gp");

        private static string PaletteFile => Path.Combine(AppContext.BaseDirectory, "palette", "0", "agew_1.pal");

        private static readonly FilePickerFileType GameFileType = new FilePickerFileType("GP sprites and game archives")
        {
            Patterns = new[] { "*.gp", "*.GP", "*.gsc", "*.GSC" },
        };

        private static readonly string[] PreferredStates = { "MOTION_L+MOTION_R", "STAND" };

        private readonly DispatcherTimer _timer = new DispatcherTimer();
        private readonly Palette _palette = Palette.Load(PaletteFile);
        private IReadOnlyList<RenderedImage> _images = Array.Empty<RenderedImage>();
        private int _current = -1;

        private GameCatalog _catalog;
        private GameObject _unit;
        private GpFile _file;
        private GpDirections _fileDirections;
        private GpDirections _directions;

        // The field: the unit, whether it is selected, and where its feet are in the images shown.
        private FieldUnit _fieldUnit;
        private bool _unitSelected;
        private Size _selectionSize = new Size(32, 14);
        private PixelPoint _anchor;

        private bool _updatingControls;
        private int _renderVersion;
        private int _loadCount;
        private bool _loading;

        /// <summary>Completes when the last requested file, unit or setting has been rendered and shown.</summary>
        public Task RenderingCompleted { get; private set; } = Task.CompletedTask;

        /// <summary>The sprite, archive or folder opened last, or null.</summary>
        public string CurrentFile { get; private set; }

        /// <summary>Game objects found in the game data next to the opened file, or null.</summary>
        public GameCatalog Catalog => _catalog;

        /// <summary>The unit shown, or null when showing a sprite file.</summary>
        public GameObject CurrentUnit => _unit;

        /// <summary>The unit's state shown, or null.</summary>
        public GameObjectState CurrentState => _unit == null ? null : (StateBox.SelectedItem as StateOption)?.State;

        public bool IsPlaying => _timer.IsEnabled;

        /// <summary>Number of the frame (or, by direction, the pose) currently shown, or -1.</summary>
        public int CurrentFrame => _current >= 0 ? _images[_current].Number : -1;

        /// <summary>Number of frames (or, by direction, poses) that can be shown.</summary>
        public int FrameCount => _images.Count;

        /// <summary>Directions of the sprite or unit state shown, or null if it has none.</summary>
        public GpDirections Directions => _directions;

        /// <summary>Whether a direction is applied: always for units, optionally for sprite files.</summary>
        public bool ShowsDirection => _directions != null && (_unit != null || ByDirectionBox.IsChecked == true);

        /// <summary>The selected direction (0 = south, clockwise), or -1.</summary>
        public int CurrentDirection => ShowsDirection ? DirectionBox.SelectedIndex : -1;

        /// <summary>Player color the frames are rendered with.</summary>
        public NationColorOffset PlayerColor => (PlayerColorBox.SelectedItem as PlayerColorOption)?.Offset ?? NationColorOffset.Blue;

        /// <summary>Whether the unit is shown on the field, where it can be selected and sent around.</summary>
        public bool IsFieldMode => FieldButton.IsChecked == true && _unit != null;

        /// <summary>The unit on the field (position, target, heading), or null outside field mode.</summary>
        public FieldUnit FieldUnit => IsFieldMode ? _fieldUnit : null;

        /// <summary>Whether the unit on the field is selected (and so follows right-clicks).</summary>
        public bool IsUnitSelected => IsFieldMode && _unitSelected;

        public MainWindow() : this(DefaultGpFile)
        {
        }

        /// <param name="path">A .gp sprite, a .gsc archive or a folder of extracted game files.</param>
        public MainWindow(string path)
        {
            InitializeComponent();

            var colorOptions = NationColorOffset.All
                .Select(color => new PlayerColorOption(color.Name, color.Offset, SwatchOf(color.Offset)))
                .ToList();
            PlayerColorBox.ItemsSource = colorOptions;
            PlayerColorBox.SelectedItem = colorOptions.First(option => option.Offset.Offset == NationColorOffset.Blue.Offset);

            PlayerColorBox.SelectionChanged += (sender, args) => Rerender(resetRange: false);
            DirectionBox.SelectionChanged += (sender, args) =>
            {
                // Picking a direction for a standing unit on the field turns it.
                if (!_updatingControls && IsFieldMode && !_fieldUnit.IsWalking && _directions != null && DirectionBox.SelectedIndex >= 0)
                {
                    _fieldUnit.TurnTo(_directions.AngleOf(DirectionBox.SelectedIndex));
                }

                Rerender(resetRange: false);
            };
            FieldButton.IsCheckedChanged += (sender, args) =>
            {
                if (!_updatingControls)
                {
                    RenderingCompleted = ApplyFieldMode();
                }
            };
            Field.PointerPressed += OnFieldPressed;
            ByDirectionBox.IsCheckedChanged += (sender, args) =>
            {
                DirectionBox.IsEnabled = ByDirectionBox.IsChecked == true;
                Rerender(resetRange: true);
            };
            UnitBox.SelectionChanged += (sender, args) =>
            {
                if (!_updatingControls && UnitBox.SelectedItem is GameObject unit)
                {
                    RenderingCompleted = ShowUnitAsync(unit);
                }
            };
            StateBox.SelectionChanged += (sender, args) =>
            {
                if (!_updatingControls && _unit != null)
                {
                    UpdateDirections();
                    Rerender(resetRange: true);
                }
            };

            OpenButton.Click += async (sender, args) => await PickFileAsync();
            PlayButton.Click += (sender, args) => TogglePlayback();
            PreviousButton.Click += (sender, args) => StepFrame(-1);
            NextButton.Click += (sender, args) => StepFrame(1);
            FramesPerSecond.ValueChanged += (sender, args) => UpdateTimerInterval();

            AddHandler(DragDrop.DragOverEvent, OnDragOver);
            AddHandler(DragDrop.DropEvent, OnDrop);

            _timer.Tick += (sender, args) => Tick();
            UpdateTimerInterval();

            Opened += (sender, args) => LoadFile(path);
            Closed += (sender, args) =>
            {
                _timer.Stop();
                _catalog?.Dispose();
            };
        }

        /// <summary>
        /// Opens a .gp sprite (and the game data next to it, if any), a .gsc archive or a folder of extracted
        /// game files; <see cref="RenderingCompleted"/> completes when it is shown.
        /// </summary>
        public Task LoadFile(string path)
        {
            RenderingCompleted = LoadAsync(path);
            return RenderingCompleted;
        }

        /// <summary>Shows a unit of the current game data, by object or descriptor name (e.g. <c>BA1</c>).</summary>
        public Task SelectUnit(string descriptorName)
        {
            var unit = _catalog?.Objects.FirstOrDefault(o => o.Descriptor.FileName.Equals(descriptorName, StringComparison.OrdinalIgnoreCase))
                       ?? throw new ArgumentException("No such unit: " + descriptorName, nameof(descriptorName));
            UnitBox.SelectedItem = unit;
            return RenderingCompleted;
        }

        /// <summary>Shows another state of the current unit, by code (e.g. <c>DEATH</c>).</summary>
        public Task SelectState(string code)
        {
            StateBox.SelectedItem = StateBox.Items.OfType<StateOption>()
                .FirstOrDefault(option => option.State.Code.Equals(code, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException("No such state: " + code, nameof(code));
            return RenderingCompleted;
        }

        /// <summary>Re-renders in another player color, keeping frame, loop range and playback.</summary>
        public Task SetPlayerColor(NationColorOffset color)
        {
            PlayerColorBox.SelectedItem = PlayerColorBox.Items
                .OfType<PlayerColorOption>()
                .First(option => option.Offset.Offset == color.Offset);
            return RenderingCompleted;
        }

        /// <summary>Shows another direction, keeping frame, loop range and playback.</summary>
        public Task SetDirection(int direction)
        {
            ByDirectionBox.IsChecked = true;
            DirectionBox.SelectedIndex = direction;
            return RenderingCompleted;
        }

        /// <summary>For sprite files: switches between the poses of one direction and all frames in file order.</summary>
        public Task SetShowByDirection(bool byDirection)
        {
            ByDirectionBox.IsChecked = byDirection;
            return RenderingCompleted;
        }

        /// <summary>
        /// Puts the shown unit on the field (standing, facing the selected direction, and playing), or takes it
        /// back to the plain animation view.
        /// </summary>
        public Task SetFieldMode(bool on)
        {
            FieldButton.IsChecked = on;
            return RenderingCompleted;
        }

        /// <summary>
        /// One tick of the playback timer: the next frame, and on the field one step of a walking unit,
        /// which stands again when it arrives.
        /// </summary>
        public void Tick()
        {
            StepFrame(1);

            if (IsFieldMode && _fieldUnit.IsWalking)
            {
                if (_fieldUnit.Step())
                {
                    RenderingCompleted = ShowFieldStateAsync(StandStateOf(_unit));
                }

                UpdateFieldView();
            }
        }

        /// <summary>Shows the image at a position (0 to <see cref="FrameCount"/> - 1), or nothing for -1.</summary>
        public void ShowFrameAt(int index)
        {
            _current = index >= 0 && index < _images.Count ? index : -1;
            ImageView.Source = _current >= 0 ? _images[_current].Bitmap : null;
            UpdateFieldView();
            FrameText.Text = _current >= 0
                ? (_unit == null && ShowsDirection ? "Pose " : "Frame ") + _images[_current].Number + " / " + _images[^1].Number
                : "";
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

        /// <summary>The state a unit stands in: STAND, or the first state if it has none.</summary>
        private static GameObjectState StandStateOf(GameObject unit) =>
            unit.States.FirstOrDefault(state => state.Code == "STAND")
            ?? unit.States.FirstOrDefault(state => state.Code.StartsWith("STAND", StringComparison.Ordinal))
            ?? unit.States[0];

        /// <summary>The state a unit walks in: both walk steps, one of them, or standing if it cannot walk.</summary>
        private static GameObjectState WalkStateOf(GameObject unit) =>
            unit.States.FirstOrDefault(state => state.Code == "MOTION_L+MOTION_R")
            ?? unit.States.FirstOrDefault(state => state.Code == "MOTION_L")
            ?? unit.States.FirstOrDefault(state => state.Code.StartsWith("MOTION", StringComparison.Ordinal))
            ?? StandStateOf(unit);

        private void UpdateFieldVisibility()
        {
            ImageScroller.IsVisible = !IsFieldMode;
            Field.IsVisible = IsFieldMode;
        }

        /// <summary>Enters or leaves the field after the Field button changed.</summary>
        private Task ApplyFieldMode()
        {
            UpdateFieldVisibility();
            _unitSelected = false;

            if (!IsFieldMode)
            {
                _fieldUnit?.Stop();
                // Back to the gray background of the plain view.
                return (_file != null || _unit != null) && !_loading ? RenderAsync(resetRange: false) : RenderingCompleted;
            }

            var heading = _directions != null && DirectionBox.SelectedIndex >= 0 ? _directions.AngleOf(DirectionBox.SelectedIndex) : 0;
            var area = new Rect(ViewArea.Bounds.Size);
            if (_fieldUnit == null || !area.Contains(_fieldUnit.Position))
            {
                _fieldUnit = new FieldUnit(area.Width > 0 && area.Height > 0
                    ? new Point(Math.Round(area.Width / 2), Math.Round(area.Height * 0.6))
                    : new Point(240, 200));
            }

            _fieldUnit.Stop();
            _fieldUnit.TurnTo(heading);
            Play();
            return ShowFieldStateAsync(StandStateOf(_unit), forceRender: true);
        }

        /// <summary>
        /// Shows the unit on the field in a state, facing its heading. A new state starts from its first
        /// frame; turning while walking keeps the step the unit is in.
        /// </summary>
        private Task ShowFieldStateAsync(GameObjectState state, bool forceRender = false)
        {
            var stateChanged = !ReferenceEquals(CurrentState, state);
            _updatingControls = true;
            try
            {
                var option = StateBox.Items.OfType<StateOption>().FirstOrDefault(o => ReferenceEquals(o.State, state));
                if (option != null)
                {
                    StateBox.SelectedItem = option;
                }
            }
            finally
            {
                _updatingControls = false;
            }

            if (stateChanged)
            {
                UpdateDirections();
            }

            var direction = _directions?.DirectionAt(_fieldUnit.Heading) ?? -1;
            var turned = direction >= 0 && direction != DirectionBox.SelectedIndex;
            if (turned)
            {
                _updatingControls = true;
                DirectionBox.SelectedIndex = direction;
                _updatingControls = false;
            }

            if (!stateChanged && !turned && !forceRender)
            {
                return RenderingCompleted;
            }

            return RenderAsync(resetRange: stateChanged || forceRender);
        }

        private void OnFieldPressed(object sender, PointerPressedEventArgs e)
        {
            if (!IsFieldMode)
            {
                return;
            }

            var point = e.GetCurrentPoint(Field);
            if (point.Properties.IsLeftButtonPressed)
            {
                // Left-click selects the unit, or deselects it when clicking elsewhere.
                _unitSelected = HitsUnit(point.Position);
                if (_unitSelected)
                {
                    UpdateSelectionSize();
                }

                UpdateFieldView();
                e.Handled = true;
            }
            else if (point.Properties.IsRightButtonPressed)
            {
                if (_unitSelected)
                {
                    RenderingCompleted = SendUnitTo(point.Position);
                }

                e.Handled = true;
            }
        }

        /// <summary>Sends the unit on the field to a point: it turns, walks there and stands again.</summary>
        private Task SendUnitTo(Point target)
        {
            var size = Field.Bounds.Size;
            target = new Point(Math.Clamp(target.X, 0, Math.Max(size.Width, 0)), Math.Clamp(target.Y, 0, Math.Max(size.Height, 0)));

            _fieldUnit.MoveTo(target);
            Play();
            var shown = ShowFieldStateAsync(_fieldUnit.IsWalking ? WalkStateOf(_unit) : StandStateOf(_unit));
            UpdateFieldView();
            return shown;
        }

        /// <summary>Where the unit's image is on the field: its feet (the anchor) at the unit's position.</summary>
        private Point FieldImageOrigin() => new Point(
            Math.Round(_fieldUnit.Position.X) - _anchor.X,
            Math.Round(_fieldUnit.Position.Y) - _anchor.Y);

        /// <summary>Whether a point of the field is on the unit's drawn pixels (with a little slack).</summary>
        private bool HitsUnit(Point point)
        {
            if (_current >= 0 && _images[_current].Opaque is { } opaque)
            {
                var origin = FieldImageOrigin();
                return new Rect(origin.X + opaque.X, origin.Y + opaque.Y, opaque.Width, opaque.Height).Inflate(4).Contains(point);
            }

            var way = point - _fieldUnit.Position;
            return way.X * way.X + way.Y * way.Y <= 16 * 16;
        }

        /// <summary>Sizes the selection ring under the unit's feet to the unit's width.</summary>
        private void UpdateSelectionSize()
        {
            var width = _current >= 0 && _images[_current].Opaque is { } opaque ? opaque.Width : 32;
            var ring = Math.Clamp(width * 0.9, 20, 160);
            _selectionSize = new Size(Math.Round(ring), Math.Round(Math.Max(8, ring * 0.45)));
        }

        /// <summary>Places the unit's current frame, the selection ring and the target marker on the field.</summary>
        private void UpdateFieldView()
        {
            if (!IsFieldMode || _fieldUnit == null)
            {
                return;
            }

            FieldImage.Source = _current >= 0 ? _images[_current].Bitmap : null;
            var origin = FieldImageOrigin();
            Canvas.SetLeft(FieldImage, origin.X);
            Canvas.SetTop(FieldImage, origin.Y);

            var feet = new Point(Math.Round(_fieldUnit.Position.X), Math.Round(_fieldUnit.Position.Y));
            SelectionMarker.IsVisible = _unitSelected;
            SelectionMarker.Width = _selectionSize.Width;
            SelectionMarker.Height = _selectionSize.Height;
            Canvas.SetLeft(SelectionMarker, feet.X - _selectionSize.Width / 2);
            Canvas.SetTop(SelectionMarker, feet.Y - _selectionSize.Height / 2);

            var target = _fieldUnit.Target;
            TargetMarker.IsVisible = _unitSelected && target != null;
            if (target is { } t)
            {
                Canvas.SetLeft(TargetMarker, t.X - TargetMarker.Width / 2);
                Canvas.SetTop(TargetMarker, t.Y - TargetMarker.Height / 2);
            }

            FieldHint.Text = _unitSelected
                ? "Right-click where the unit should go. Left-click elsewhere to deselect it."
                : "Left-click the unit to select it, then right-click where it should go.";
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
                Title = "Open a GP sprite or the game's All.gsc",
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

        private static bool IsGameData(string path) =>
            Directory.Exists(path) || string.Equals(Path.GetExtension(path), ".gsc", StringComparison.OrdinalIgnoreCase);

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
            var version = ++_renderVersion;
            Pause();
            StatusText.Text = "Loading " + path + "…";
            var load = ++_loadCount;
            _loading = true;

            try
            {
                if (IsGameData(path))
                {
                    var catalog = await Task.Run(() => GameCatalog.Load(GameFiles.Open(path)));
                    if (version != _renderVersion)
                    {
                        catalog.Dispose();
                        return;
                    }

                    SetCatalog(catalog);
                    CurrentFile = path;

                    var first = catalog.Units.FirstOrDefault();
                    if (first == null)
                    {
                        StatusText.Text = "No units found in " + path;
                        return;
                    }

                    _updatingControls = true;
                    UnitBox.SelectedItem = first;
                    _updatingControls = false;
                    await ShowUnitAsync(first);
                    return;
                }

                // A sprite file, with the game data around it if there is any.
                var existing = _catalog;
                var currentSource = existing?.Files.Source;
                var (file, catalogToUse, newCatalog, directions) = await Task.Run(() =>
                {
                    var loaded = GpFile.Load(path);
                    var folder = DescriptorFolderOf(path);
                    GameCatalog found = null;
                    if (folder != null && !string.Equals(Path.GetFullPath(folder), currentSource, StringComparison.OrdinalIgnoreCase))
                    {
                        found = GameCatalog.Load(new FolderGameFiles(folder));
                    }

                    var catalog = found ?? (folder != null ? existing : null);
                    var name = Path.GetFileNameWithoutExtension(path);
                    var detected = (catalog == null ? null : GpDirections.FromDescriptors(name, loaded, catalog.Objects.Select(o => o.Descriptor)))
                                   ?? GpDirections.FromFrames(loaded);
                    return (loaded, catalog, found, detected);
                });

                if (version != _renderVersion)
                {
                    newCatalog?.Dispose();
                    return;
                }

                if (newCatalog != null)
                {
                    SetCatalog(newCatalog);
                }
                else if (catalogToUse == null)
                {
                    SetCatalog(null);
                }

                CurrentFile = path;
                _unit = null;
                _file = file;
                _fileDirections = directions;

                _updatingControls = true;
                UnitBox.SelectedItem = null;
                StateBox.ItemsSource = null;
                // The field needs a unit.
                FieldButton.IsChecked = false;
                FieldButton.IsEnabled = false;
                _updatingControls = false;
                UpdateFieldVisibility();

                UpdateDirections();
                await RenderAsync(resetRange: true);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                if (version == _renderVersion)
                {
                    StatusText.Text = "Failed to open " + path + ": " + ex.Message;
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

        private void SetCatalog(GameCatalog catalog)
        {
            if (ReferenceEquals(catalog, _catalog))
            {
                return;
            }

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
                    : units.Count + " units described in " + catalog.Files.Source + " (type to search)");
            }
            finally
            {
                _updatingControls = false;
            }
        }

        /// <summary>Switches to a unit, keeping the state (if it has it), direction and color.</summary>
        private Task ShowUnitAsync(GameObject unit)
        {
            var previousCode = CurrentState?.Code;
            _unit = unit;
            FieldButton.IsEnabled = true;

            // On the field, another unit takes over where the last one was, standing.
            var onField = IsFieldMode;
            if (onField)
            {
                _fieldUnit.Stop();
                previousCode = StandStateOf(unit).Code;
            }

            var options = unit.States.Select(state => new StateOption(state)).ToList();
            var selected = options.FirstOrDefault(option => option.State.Code == previousCode)
                           ?? PreferredStates.Select(code => options.FirstOrDefault(option => option.State.Code == code)).FirstOrDefault(option => option != null)
                           ?? options[0];

            _updatingControls = true;
            try
            {
                StateBox.ItemsSource = options;
                StateBox.SelectedItem = selected;
            }
            finally
            {
                _updatingControls = false;
            }

            UpdateDirections();
            if (onField && _directions != null)
            {
                _updatingControls = true;
                DirectionBox.SelectedIndex = _directions.DirectionAt(_fieldUnit.Heading);
                _updatingControls = false;
            }

            UpdateFieldVisibility();
            return RenderAsync(resetRange: true);
        }

        /// <summary>
        /// Shows the direction picker for the sprite or the unit's state, keeping the angle when possible.
        /// </summary>
        private void UpdateDirections()
        {
            GpDirections directions;
            if (_unit != null)
            {
                var state = CurrentState;
                directions = state != null && state.Directions > 1
                    ? new GpDirections(state.Directions, 1, 0, GpDirectionsSource.Descriptor, _unit.Descriptor.FileName)
                    : null;
            }
            else
            {
                directions = _fileDirections;
            }

            var angle = _directions != null && DirectionBox.SelectedIndex >= 0 ? _directions.AngleOf(DirectionBox.SelectedIndex) : 0;
            var sameItems = _directions != null && directions != null && _directions.Count == directions.Count;

            _updatingControls = true;
            try
            {
                _directions = directions;
                DirectionPanel.IsVisible = directions != null;
                ByDirectionBox.IsVisible = _unit == null;
                DirectionLabel.IsVisible = _unit != null;
                DirectionBox.IsEnabled = _unit != null || ByDirectionBox.IsChecked == true;

                if (directions == null)
                {
                    DirectionBox.ItemsSource = Array.Empty<DirectionOption>();
                    return;
                }

                if (!sameItems)
                {
                    DirectionBox.ItemsSource = Enumerable.Range(0, directions.Count)
                        .Select(i => new DirectionOption(i, LabelOf(directions, i)))
                        .ToArray();
                    DirectionBox.SelectedIndex = directions.DirectionAt(angle);
                }

                ToolTip.SetTip(DirectionBox, _unit != null
                    ? directions.Count + " directions" + (directions.IsMirrored ? "; the eastern half is mirrored" : "")
                    : directions.Count + " directions, " + directions.Poses + " poses each"
                      + (directions.Source == GpDirectionsSource.Descriptor
                          ? ", from " + directions.DescriptorName + ".MD"
                          : ", recognized from the frames")
                      + (directions.IsMirrored ? "; the eastern half is mirrored" : ""));
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

        /// <summary>Re-renders after a change of color, direction, state or mode.</summary>
        private void Rerender(bool resetRange)
        {
            // While a file loads, it picks up the change when it renders.
            if ((_file != null || _unit != null) && !_loading && !_updatingControls)
            {
                RenderingCompleted = RenderAsync(resetRange);
            }
        }

        /// <summary>
        /// Renders off the UI thread and shows the result. With <paramref name="resetRange"/>, the loop range
        /// covers everything and the first frame is shown; otherwise the shown frame, loop range and playback stay.
        /// </summary>
        private async Task RenderAsync(bool resetRange)
        {
            var version = ++_renderVersion;
            var request = new RenderRequest(
                _unit == null ? _file : null,
                _unit != null ? _catalog : null,
                _unit,
                CurrentState,
                ShowsDirection ? _directions : null,
                Math.Max(DirectionBox.SelectedIndex, 0),
                PlayerColor,
                Transparent: IsFieldMode);

            var (results, anchor) = await Task.Run(() => Render(request, _palette));

            // A newer file, unit or setting was requested while this one was rendering.
            if (version != _renderVersion)
            {
                return;
            }

            var images = results
                .Where(result => result.Image != null)
                .Select(result => new RenderedImage(result.Number, result.Image.ToWriteableBitmap(), result.Opaque))
                .ToList();

            StatusText.Text = StatusOf(results);

            var shownFrame = CurrentFrame;
            _images = images;
            _anchor = anchor;

            if (_unit != null)
            {
                Title = "GP Viewer - " + Path.GetFileName(_catalog.Files.Source) + " - " + _unit.Label;
                FileNameText.Text = Path.GetFileName(_catalog.Files.Source);
            }
            else
            {
                Title = "GP Viewer - " + Path.GetFileName(CurrentFile);
                FileNameText.Text = Path.GetFileName(CurrentFile) + (_fileDirections == null
                    ? " (" + _file.Frames.Count + " frames)"
                    : " (" + _fileDirections.Poses + " poses × " + _fileDirections.Count + " directions)");
            }

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

        /// <summary>What to render: a sprite file, or a unit's state from the catalog.</summary>
        private sealed record RenderRequest(
            GpFile File, GameCatalog Catalog, GameObject Unit, GameObjectState State,
            GpDirections Directions, int Direction, NationColorOffset Color, bool Transparent);

        /// <summary>One image to render: a frame of a sprite placed on the shared canvas.</summary>
        private sealed record FrameJob(int Number, GpFile File, int Frame, bool Mirror, int OffsetX, int OffsetY, string Error = null);

        /// <summary>Renders the frames; also returns where the anchor (the unit's feet) is in them.</summary>
        private static (RenderResult[] Results, PixelPoint Anchor) Render(RenderRequest request, Palette palette)
        {
            var renderer = new GpRenderer(palette)
            {
                PlayerColor = request.Color,
                // On the field, the field shows through.
                Background = request.Transparent ? Color.Transparent : Color.FromArgb(255, 0x80, 0x80, 0x80),
            };

            var (jobs, width, height, anchor) = request.Unit != null ? PlanUnit(request) : PlanFile(request);

            var results = jobs.Select(job =>
                {
                    if (job.Error != null)
                    {
                        return new RenderResult(job.Number, null, job.Error, 0);
                    }

                    try
                    {
                        var rendered = renderer.Render(job.File, job.File.Frames[job.Frame], width, height, job.OffsetX, job.Mirror, job.OffsetY);
                        return new RenderResult(job.Number, rendered.Image, null, rendered.SkippedParts.Count,
                            request.Transparent ? OpaqueBounds(rendered.Image) : null);
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
        /// All frames of a sprite, or the poses of one direction. Frames share one canvas (for directions,
        /// centred on the anchor), so the animation does not jump when playing or turning.
        /// </summary>
        private static (IReadOnlyList<FrameJob> Jobs, int Width, int Height, PixelPoint Anchor) PlanFile(RenderRequest request)
        {
            var file = request.File;
            var directions = request.Directions;
            var right = file.Frames.Select(frame => frame.Width).DefaultIfEmpty(1).Max();
            var height = file.Frames.Select(frame => frame.Height).DefaultIfEmpty(1).Max();

            if (directions == null)
            {
                return (file.Frames.Select(frame => new FrameJob(frame.Index, file, frame.Index, false, 0, 0)).ToList(), right, height, default);
            }

            var left = file.Frames.SelectMany(frame => frame.Parts).Select(part => part.X).DefaultIfEmpty(0).Min();
            var half = Math.Max(Math.Max(directions.AnchorX - Math.Min(left, 0), right - directions.AnchorX), 1);
            var offset = half - directions.AnchorX;

            var jobs = Enumerable.Range(0, directions.Poses)
                .Select(pose =>
                {
                    var (frame, mirrored) = directions.FrameOf(pose, request.Direction);
                    return new FrameJob(pose, file, frame, mirrored, offset, 0);
                })
                .ToList();

            return (jobs, 2 * half, height, new PixelPoint(half, 0));
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

            var sprites = new Dictionary<int, (GpFile File, int AnchorX, int AnchorY)>();
            foreach (var (index, directions) in spriteDirections)
            {
                var descriptor = unit.Sprite(index);
                var file = descriptor == null ? null : catalog.Sprite(descriptor.FileName);
                if (file == null || file.Frames.Count == 0)
                {
                    continue;
                }

                sprites[index] = (file,
                    descriptor.AnchorX ?? GpDirections.EstimateAnchorX(file, Math.Min(directions, file.Frames.Count)),
                    descriptor.AnchorY ?? 0);
            }

            // Canvas: symmetric around the anchor horizontally (for mirroring), tall enough for every sprite.
            int half = 1, top = 0, bottom = 1;
            foreach (var (file, anchorX, anchorY) in sprites.Values)
            {
                foreach (var part in file.Frames.SelectMany(frame => frame.Parts))
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
                        return new FrameJob(number, null, 0, false, 0, 0, "sprite " + name + " is missing or not a GP file");
                    }

                    var stored = 0;
                    var mirrored = false;
                    if (stateDirections != null)
                    {
                        mirrored = request.Direction >= stateDirections.StoredDirections;
                        stored = mirrored ? stateDirections.Count - request.Direction : request.Direction;
                    }

                    var index = frame.Pose * state.Directions + stored;
                    if (index >= sprite.File.Frames.Count)
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

    /// <param name="Opaque">Where the drawn pixels are, for images rendered for the field.</param>
    public sealed record RenderedImage(int Number, Bitmap Bitmap, PixelRect? Opaque = null)
    {
        public override string ToString() => Number.ToString();
    }

    public sealed record PlayerColorOption(string Name, NationColorOffset Offset, IBrush Swatch);

    public sealed record DirectionOption(int Index, string Label);

    /// <summary>A unit state in the picker.</summary>
    public sealed record StateOption(GameObjectState State)
    {
        public override string ToString() => State.Label + "  ·  " + State.Frames.Count + " frames";
    }
}
