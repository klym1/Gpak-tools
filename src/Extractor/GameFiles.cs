using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GPExtractor
{
    /// <summary>
    /// The game's data files: an extracted folder or a <c>.gsc</c> archive. Paths use <c>\</c> as the separator.
    /// </summary>
    public abstract class GameFiles : IDisposable
    {
        private Dictionary<string, string> _byName;

        /// <summary>Where the files come from (folder or archive path).</summary>
        public abstract string Source { get; }

        /// <summary>Relative paths of all files.</summary>
        public abstract IReadOnlyList<string> Paths { get; }

        public abstract byte[] Read(string path);

        /// <summary>
        /// Finds a file by name without folder, e.g. <c>ba1g</c> + <c>.gp</c>, case-insensitively.
        /// Files in the top folder win over files in subfolders. Returns null if there is none.
        /// </summary>
        public string Find(string fileName, string extension)
        {
            if (_byName == null)
            {
                var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var path in Paths.OrderBy(path => path.Count(c => c == '\\')).ThenBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    byName.TryAdd(path.Substring(path.LastIndexOf('\\') + 1), path);
                }

                _byName = byName;
            }

            return _byName.TryGetValue(fileName + extension, out var found) ? found : null;
        }

        /// <summary>Opens a <c>.gsc</c> archive, or a folder of extracted files.</summary>
        public static GameFiles Open(string path)
        {
            return Directory.Exists(path)
                ? new FolderGameFiles(path)
                : new ArchiveGameFiles(path);
        }

        public virtual void Dispose()
        {
        }
    }

    public sealed class FolderGameFiles : GameFiles
    {
        private readonly string _root;

        public FolderGameFiles(string root)
        {
            _root = Path.GetFullPath(root);
            var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
            Paths = Directory.EnumerateFiles(_root, "*", options)
                .Select(path => Path.GetRelativePath(_root, path).Replace(Path.DirectorySeparatorChar, '\\'))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public override string Source => _root;

        public override IReadOnlyList<string> Paths { get; }

        public override byte[] Read(string path)
        {
            return File.ReadAllBytes(Path.Combine(_root, path.Replace('\\', Path.DirectorySeparatorChar)));
        }
    }

    public sealed class ArchiveGameFiles : GameFiles
    {
        private readonly GscArchive _archive;
        private readonly Dictionary<string, GscEntry> _entries;
        private readonly object _lock = new object();

        public ArchiveGameFiles(string archivePath)
        {
            Source = Path.GetFullPath(archivePath);
            _archive = GscArchive.Open(archivePath);
            _entries = new Dictionary<string, GscEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in _archive.Entries)
            {
                _entries.TryAdd(entry.Name, entry);
            }

            Paths = _entries.Keys.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public override string Source { get; }

        public override IReadOnlyList<string> Paths { get; }

        public override byte[] Read(string path)
        {
            if (!_entries.TryGetValue(path, out var entry))
            {
                throw new FileNotFoundException("Not in the archive: " + path, path);
            }

            // The archive reads from one stream.
            lock (_lock)
            {
                return _archive.ReadAllBytes(entry);
            }
        }

        public override void Dispose()
        {
            _archive.Dispose();
        }
    }
}
