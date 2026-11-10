using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Viewer
{
    /// <summary>
    /// Plays wave files by running the system's player for each (afplay on macOS, paplay or aplay on Linux).
    /// Different files overlap; a file that is still playing is not started again.
    /// </summary>
    public sealed class CommandSoundPlayer : ISoundPlayer, IDisposable
    {
        private const int MaxPlaying = 16;

        private readonly string _command;
        private readonly Dictionary<string, Process> _playing = new Dictionary<string, Process>(StringComparer.Ordinal);
        private bool _broken;

        public CommandSoundPlayer(string command)
        {
            _command = command;
        }

        public void Play(string path)
        {
            if (_broken)
            {
                return;
            }

            foreach (var finished in _playing.Where(pair => pair.Value.HasExited).ToList())
            {
                finished.Value.Dispose();
                _playing.Remove(finished.Key);
            }

            if (_playing.ContainsKey(path) || _playing.Count >= MaxPlaying)
            {
                return;
            }

            try
            {
                var start = new ProcessStartInfo(_command) { UseShellExecute = false, CreateNoWindow = true };
                start.ArgumentList.Add(path);
                _playing[path] = Process.Start(start);
            }
            catch (Exception ex) when (ex is Win32Exception || ex is InvalidOperationException || ex is IOException)
            {
                // The player is not there after all: stay silent rather than try for every sound.
                _broken = true;
            }
        }

        public void Dispose()
        {
            foreach (var process in _playing.Values)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                }
                catch (Exception ex) when (ex is Win32Exception || ex is InvalidOperationException)
                {
                }

                process.Dispose();
            }

            _playing.Clear();
        }
    }

    public static class SoundPlayers
    {
        private sealed class Silent : ISoundPlayer
        {
            public void Play(string path)
            {
            }
        }

        /// <summary>What plays sounds on this system: Windows' own interface, or the system's player command.</summary>
        public static ISoundPlayer ForThisSystem()
        {
            if (OperatingSystem.IsWindows())
            {
                return new SoundPlayer();
            }

            var platform = OperatingSystem.IsMacOS() ? OSPlatform.OSX : OSPlatform.Linux;
            var command = CommandFor(platform, IsOnPath);
            return command == null ? new Silent() : new CommandSoundPlayer(command);
        }

        /// <summary>
        /// The command that plays a wave file given as its argument: afplay on macOS; on Linux paplay
        /// (PulseAudio, PipeWire) or else aplay (ALSA), whichever is installed. Null if there is none.
        /// </summary>
        public static string CommandFor(OSPlatform platform, Func<string, bool> isInstalled)
        {
            var candidates = platform == OSPlatform.OSX ? new[] { "afplay" }
                : platform == OSPlatform.Linux ? new[] { "paplay", "aplay" }
                : Array.Empty<string>();

            return candidates.FirstOrDefault(isInstalled);
        }

        private static bool IsOnPath(string command)
        {
            var folders = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            return folders.Any(folder => File.Exists(Path.Combine(folder, command)));
        }
    }
}
