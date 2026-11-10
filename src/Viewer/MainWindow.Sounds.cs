using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using GPExtractor;

namespace Viewer
{
    /// <summary>The sounds of units and their weapons.</summary>
    public partial class MainWindow
    {
        /// <summary>Plays the sounds the unit's state has at the frame it is in now.</summary>
        private void PlaySounds(FieldUnitView unit)
        {
            if (SoundBox.IsChecked != true || unit.State.Frames.Count == 0)
            {
                return;
            }

            var key = (unit.Kind, unit.State);
            if (!_stateSounds.TryGetValue(key, out var sounds))
            {
                sounds = _catalog.SoundsOf(unit.Kind, unit.State);
                _stateSounds[key] = sounds;
            }

            var frame = unit.Pose % unit.State.Frames.Count;
            foreach (var (_, sound, impact) in sounds.Where(sound => sound.Frame == frame))
            {
                if (impact)
                {
                    _impacts.Add((unit.ShotTicks > 0 ? unit.ShotTicks : TicksToImpact, sound));
                }
                else
                {
                    Play(sound);
                }
            }
        }

        /// <summary>Plays the impacts whose time has come.</summary>
        private void PlayImpacts()
        {
            for (var i = _impacts.Count - 1; i >= 0; i--)
            {
                var (ticksLeft, sound) = _impacts[i];
                if (ticksLeft > 1)
                {
                    _impacts[i] = (ticksLeft - 1, sound);
                    continue;
                }

                _impacts.RemoveAt(i);
                if (SoundBox.IsChecked == true)
                {
                    Play(sound);
                }
            }
        }

        /// <summary>Plays a sound by its name in <c>SOUNDLIST.TXT</c>, if the name is given and known.</summary>
        private void Play(string name)
        {
            if (SoundBox.IsChecked == true && name != null && _catalog.Sounds.Find(name) is { } sound)
            {
                Play(sound);
            }
        }

        /// <summary>Plays the first sound of its kind that a selected unit has, e.g. its <c>ORDER_SOUND</c>.</summary>
        private void PlayOfSelected(Func<GpDescriptor, string> sound) =>
            Play(SelectedFieldUnits.Select(unit => sound(unit.Kind.Descriptor)).FirstOrDefault(name => name != null));

        private void Play(GameSound sound)
        {
            if (sound.Files.Count == 0)
            {
                return;
            }

            var file = sound.Files[_random.Next(sound.Files.Count)];
            var path = SoundPath(file);
            if (path != null)
            {
                SoundPlayer.Play(path);
            }
            else if (StatusText.Text.Length == 0)
            {
                StatusText.Text = "No sound: " + file + " was not found around " + _catalog.Files.Source
                    + ". The sounds are in the game's Sound folder; open the All.gsc next to it to hear them.";
            }
        }

        /// <summary>A wave file of the sound list in the game folder: next to the archive, or around an extracted folder.</summary>
        private string SoundPath(string file)
        {
            var source = _catalog.Files.Source;
            var folders = new[] { System.IO.Path.GetDirectoryName(source), source, System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(source) ?? "") };

            return folders
                .Where(folder => !string.IsNullOrEmpty(folder))
                .Select(folder => System.IO.Path.Combine(folder, file.Replace('\\', System.IO.Path.DirectorySeparatorChar)))
                .FirstOrDefault(System.IO.File.Exists);
        }
    }
}
