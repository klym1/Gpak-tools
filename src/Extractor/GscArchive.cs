using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GPExtractor
{
    /// <summary>
    /// One file stored in a <see cref="GscArchive"/>.
    /// </summary>
    public sealed class GscEntry
    {
        internal GscEntry(string name, uint offset, uint size, byte flag)
        {
            Name = name;
            Offset = offset;
            Size = size;
            Flag = flag;
        }

        /// <summary>Path inside the archive, with <c>\</c> as the folder separator (e.g. <c>PIECES\STNN0007.SMP</c>).</summary>
        public string Name { get; }

        /// <summary>Offset of the data, relative to the start of the data area.</summary>
        public uint Offset { get; }

        public uint Size { get; }

        /// <summary>Last byte of the directory record (0 or 1). Its meaning is unknown.</summary>
        public byte Flag { get; }

        public override string ToString() => Name;
    }

    /// <summary>
    /// Reads the <c>.gsc</c> archives used by GSC Game World games (e.g. <c>All.gsc</c> of Cossacks: European Wars).
    /// </summary>
    /// <remarks>
    /// Layout (all integers little-endian):
    /// <code>
    /// 0x00  6 bytes   signature "GSCfmt"
    /// 0x06  4 bytes   unknown
    /// 0x0A  uint32    number of files N
    /// 0x0E  N * 81    directory records
    /// ...             file data, stored back to back
    ///
    /// record:  +0  4 bytes  unknown (hash?)
    ///          +4  64 bytes file name, NUL-terminated, '\' separated
    ///          +68 uint32   bitwise NOT of the data offset (relative to the data area)
    ///          +72 uint32   size
    ///          +76 4 bytes  unknown (zero)
    ///          +80 1 byte   flag (0/1, meaning unknown)
    /// </code>
    /// Every file except GP sprites (which start with "GPAK") is obfuscated by XOR-ing each byte with 0x78.
    /// </remarks>
    public sealed class GscArchive : IDisposable
    {
        public const byte XorKey = 0x78;

        private const int HeaderSize = 0x0E;
        private const int RecordSize = 81;
        private const int NameSize = 64;
        private static readonly byte[] Signature = Encoding.ASCII.GetBytes("GSCfmt");
        private static readonly byte[] GpSignature = Encoding.ASCII.GetBytes("GPAK");

        private readonly Stream _stream;
        private readonly bool _leaveOpen;
        private readonly long _dataStart;

        public IReadOnlyList<GscEntry> Entries { get; }

        public GscArchive(Stream stream, bool leaveOpen = false)
        {
            if (!stream.CanRead || !stream.CanSeek)
            {
                throw new ArgumentException("The stream must be readable and seekable.", nameof(stream));
            }

            _stream = stream;
            _leaveOpen = leaveOpen;

            var header = new byte[HeaderSize];
            stream.Position = 0;
            stream.ReadExactly(header);

            if (!header.AsSpan(0, Signature.Length).SequenceEqual(Signature))
            {
                throw new InvalidDataException("Not a GSC archive: missing GSCfmt signature.");
            }

            var count = BitConverter.ToUInt32(header, 0x0A);
            if (count > (stream.Length - HeaderSize) / RecordSize)
            {
                throw new InvalidDataException("Corrupt GSC archive: file count " + count + " does not fit in the archive.");
            }

            var directory = new byte[count * RecordSize];
            stream.ReadExactly(directory);
            _dataStart = HeaderSize + directory.LongLength;

            var entries = new GscEntry[count];
            for (var i = 0; i < count; i++)
            {
                var record = directory.AsSpan(i * RecordSize, RecordSize);

                var nameBytes = record.Slice(4, NameSize);
                var nameLength = nameBytes.IndexOf((byte) 0);
                // Latin1 maps every byte to a char, so unusual names survive unchanged.
                var name = Encoding.Latin1.GetString(nameLength < 0 ? nameBytes : nameBytes.Slice(0, nameLength));

                var offset = ~BitConverter.ToUInt32(record.Slice(68, 4));
                var size = BitConverter.ToUInt32(record.Slice(72, 4));

                if (_dataStart + offset + size > stream.Length)
                {
                    throw new InvalidDataException("Corrupt GSC archive: data of '" + name + "' lies outside the archive.");
                }

                entries[i] = new GscEntry(name, offset, size, record[80]);
            }

            Entries = entries;
        }

        public static GscArchive Open(string path)
        {
            return new GscArchive(File.OpenRead(path));
        }

        /// <summary>Returns the decoded content of the entry.</summary>
        public byte[] ReadAllBytes(GscEntry entry)
        {
            var data = new byte[entry.Size];
            _stream.Position = _dataStart + entry.Offset;
            _stream.ReadExactly(data);

            if (!data.AsSpan().StartsWith(GpSignature))
            {
                for (var i = 0; i < data.Length; i++)
                {
                    data[i] ^= XorKey;
                }
            }

            return data;
        }

        /// <summary>
        /// Writes the entry below <paramref name="outputDirectory"/>, recreating its folders,
        /// and returns the path written.
        /// </summary>
        public string ExtractTo(GscEntry entry, string outputDirectory)
        {
            var path = GetSafeOutputPath(entry.Name, outputDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, ReadAllBytes(entry));
            return path;
        }

        /// <summary>
        /// Maps an archive name to a path inside <paramref name="outputDirectory"/>,
        /// rejecting names that would escape it (absolute paths, "..").
        /// </summary>
        public static string GetSafeOutputPath(string entryName, string outputDirectory)
        {
            var parts = entryName.Split('\\', '/');
            foreach (var part in parts)
            {
                if (part.Length == 0 || part == "." || part == ".." || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    throw new InvalidDataException("Unsafe file name in GSC archive: '" + entryName + "'.");
                }
            }

            var root = Path.GetFullPath(outputDirectory);
            var path = Path.GetFullPath(Path.Combine(root, Path.Combine(parts)));

            if (!path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Unsafe file name in GSC archive: '" + entryName + "'.");
            }

            return path;
        }

        public void Dispose()
        {
            if (!_leaveOpen)
            {
                _stream.Dispose();
            }
        }
    }
}
