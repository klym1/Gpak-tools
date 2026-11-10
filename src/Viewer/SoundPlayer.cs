using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Viewer
{
    public interface ISoundPlayer
    {
        void Play(string path);
    }

    /// <summary>
    /// Plays wave files through the Windows multimedia interface (see <see cref="SoundPlayers"/> for other systems).
    /// Different files overlap; a file that is still playing is not started again.
    /// </summary>
    public sealed class SoundPlayer : ISoundPlayer, IDisposable
    {
        private const int MaxOpenSounds = 16;

        private readonly Dictionary<string, string> _aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private int _next;

        public void Play(string path)
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            if (_aliases.TryGetValue(path, out var alias))
            {
                if (IsPlaying(alias))
                {
                    return;
                }

                Send("play " + alias + " from 0");
                return;
            }

            if (_aliases.Count >= MaxOpenSounds)
            {
                foreach (var finished in _aliases.Where(pair => !IsPlaying(pair.Value)).ToList())
                {
                    Send("close " + finished.Value);
                    _aliases.Remove(finished.Key);
                }
            }

            alias = "gpviewer" + _next++;
            if (_aliases.Count >= MaxOpenSounds || Send("open \"" + path + "\" type waveaudio alias " + alias) != 0)
            {
                return;
            }

            _aliases[path] = alias;
            Send("play " + alias);
        }

        public void Dispose()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            foreach (var alias in _aliases.Values)
            {
                Send("close " + alias);
            }

            _aliases.Clear();
        }

        private static bool IsPlaying(string alias)
        {
            var mode = new StringBuilder(32);
            return Send("status " + alias + " mode", mode) == 0 && mode.ToString() == "playing";
        }

        private static int Send(string command, StringBuilder result = null)
        {
            return mciSendString(
                  command
                , result
                , result?.Capacity ?? 0
                , IntPtr.Zero
            );
        }

        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        private static extern int mciSendString(
              string command
            , StringBuilder result
            , int resultLength
            , IntPtr callback
        );
    }
}
