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

        private static byte[] BuildArchive(params (string Name, byte[] Content, bool Obfuscate)[] files) => GpTestFiles.Archive(files);

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
