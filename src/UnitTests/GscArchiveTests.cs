using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using GPExtractor;
using NUnit.Framework;

namespace UnitTests
{
    [TestFixture]
    public class GscArchiveTests
    {
        private static readonly byte[] GpData = Encoding.ASCII.GetBytes("GPAK\x01\x02\x03");
        private static readonly byte[] TextData = Encoding.ASCII.GetBytes("STONE1 4\r\nSound\\ResKam1.wav\r\n");

        /// <summary>Builds an archive in the same layout as Cossacks' All.gsc.</summary>
        private static byte[] BuildArchive(params (string Name, byte[] Content, bool Obfuscate)[] files)
        {
            var output = new MemoryStream();
            var writer = new BinaryWriter(output);

            writer.Write(Encoding.ASCII.GetBytes("GSCfmt"));
            writer.Write(new byte[] { 0x00, 0x02, 0xCD, 0x78 });
            writer.Write((uint) files.Length);

            uint offset = 0;
            foreach (var file in files)
            {
                writer.Write(new byte[] { 0x95, 0x7A, 0x5E, 0xC1 });
                var name = new byte[64];
                Encoding.ASCII.GetBytes(file.Name).CopyTo(name, 0);
                writer.Write(name);
                writer.Write(~offset);
                writer.Write((uint) file.Content.Length);
                writer.Write(0u);
                writer.Write((byte) 1);
                offset += (uint) file.Content.Length;
            }

            foreach (var file in files)
            {
                writer.Write(file.Obfuscate ? file.Content.Select(b => (byte) (b ^ GscArchive.XorKey)).ToArray() : file.Content);
            }

            return output.ToArray();
        }

        [Test]
        public void ReadsDirectoryAndDecodesContent()
        {
            var bytes = BuildArchive(("ADET.GP", GpData, false), ("TEXT\\SOUNDLIST.TXT", TextData, true));

            using var archive = new GscArchive(new MemoryStream(bytes));

            archive.Entries.Select(e => e.Name).Should().Equal("ADET.GP", "TEXT\\SOUNDLIST.TXT");
            archive.Entries[1].Size.Should().Be((uint) TextData.Length);
            archive.ReadAllBytes(archive.Entries[0]).Should().Equal(GpData);
            archive.ReadAllBytes(archive.Entries[1]).Should().Equal(TextData);
        }

        [Test]
        public void ExtractsIntoSubfolders()
        {
            var bytes = BuildArchive(("TEXT\\SOUNDLIST.TXT", TextData, true));
            var outputDirectory = Path.Combine(Path.GetTempPath(), "GscArchiveTests-" + Guid.NewGuid());

            try
            {
                using var archive = new GscArchive(new MemoryStream(bytes));
                var path = archive.ExtractTo(archive.Entries[0], outputDirectory);

                path.Should().Be(Path.Combine(Path.GetFullPath(outputDirectory), "TEXT", "SOUNDLIST.TXT"));
                File.ReadAllBytes(path).Should().Equal(TextData);
            }
            finally
            {
                if (Directory.Exists(outputDirectory))
                {
                    Directory.Delete(outputDirectory, true);
                }
            }
        }

        [TestCase("..\\evil.txt")]
        [TestCase("A\\..\\..\\evil.txt")]
        [TestCase("\\evil.txt")]
        [TestCase("")]
        public void RejectsNamesThatEscapeTheOutputDirectory(string name)
        {
            Action act = () => GscArchive.GetSafeOutputPath(name, Path.GetTempPath());

            act.Should().Throw<InvalidDataException>();
        }

        [Test]
        public void RejectsFilesWithoutSignature()
        {
            Action act = () => new GscArchive(new MemoryStream(new byte[64]));

            act.Should().Throw<InvalidDataException>().WithMessage("*signature*");
        }

        [Test]
        public void RejectsEntriesPointingOutsideTheArchive()
        {
            var bytes = BuildArchive(("A.TXT", TextData, true));
            Array.Resize(ref bytes, bytes.Length - 1);

            Action act = () => new GscArchive(new MemoryStream(bytes));

            act.Should().Throw<InvalidDataException>().WithMessage("*outside*");
        }
    }
}
