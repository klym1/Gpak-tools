using System;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using GPExtractor;
using NUnit.Framework;

namespace UnitTests
{
    [TestFixture]
    public class GameCatalogTests
    {
        private string _folder;

        [SetUp]
        public void CreateFolder()
        {
            _folder = Path.Combine(Path.GetTempPath(), "GameCatalogTests-" + Guid.NewGuid());
            Directory.CreateDirectory(_folder);
        }

        [TearDown]
        public void DeleteFolder()
        {
            Directory.Delete(_folder, true);
        }

        private GameFiles ExtractedFolder()
        {
            foreach (var (name, content) in GpTestFiles.UnitGameData())
            {
                File.WriteAllBytes(Path.Combine(_folder, name), content);
            }

            return GameFiles.Open(_folder);
        }

        private GameFiles Archive()
        {
            var path = Path.Combine(_folder, "All.gsc");
            File.WriteAllBytes(path, GpTestFiles.GameArchive(GpTestFiles.UnitGameData()));
            return GameFiles.Open(path);
        }

        [Test]
        public void ParsesNamesAnchorsAndRangeAnimations()
        {
            var descriptor = GpDescriptor.Parse("TST", GpTestFiles.UnitGameData()[0].Content);

            descriptor.Name.Should().Be("Testovyj");
            descriptor.DisplayName.Should().Be("Тестовый воин", "the name is stored in DOS Cyrillic (866)");
            descriptor.Sprites.Should().Equal(
                new GpDescriptorSprite(0, "tsts", 20, 10),
                new GpDescriptorSprite(1, "tstg", 30, 15));

            var animations = descriptor.Animations.ToDictionary(a => a.Name);
            animations["MOTION_L"].Frames.Should().Equal((1, 0), (1, 1));
            animations["MOTION_R"].Frames.Should().Equal((1, 2), (1, 3));
            // A descending range plays backwards.
            animations["MOTION_LB"].Frames.Should().Equal((1, 3), (1, 2));
        }

        [Test]
        public void RangeEndsBeforeItsLastPose()
        {
            // As in the game: the death animation ends where the corpse (DEATHLIE1, pose 25) begins.
            var descriptor = GpDescriptor.Parse("X", "@DEATH 9 3 0 25\r\n#DEATHLIE1 9 1 3 25\r\n");

            descriptor.Animations[0].Frames.Select(f => f.Pose).Should().Equal(Enumerable.Range(0, 25));
            descriptor.Animations[1].Frames.Should().Equal((3, 25));
        }

        [Test]
        public void DecodesWindowsCyrillicNamesToo()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var bytes = Encoding.GetEncoding(1251).GetBytes("MESSAGE Скамповей\r\n");

            GpDescriptor.Parse("SKA", bytes).DisplayName.Should().Be("Скамповей");
        }

        [Test]
        public void KeepsCyrillicLettersThatLookLikeWhitespaceInLatin1()
        {
            // In DOS Cyrillic "Е" is 0x85, which is a line break (NEL) in Latin-1.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var bytes = Encoding.GetEncoding(866).GetBytes("MESSAGE Егерь\r\n");

            GpDescriptor.Parse("LPF", bytes).DisplayName.Should().Be("Егерь");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ListsUnitsWithStatesFromFoldersAndArchives(bool archive)
        {
            using var catalog = GameCatalog.Load(archive ? Archive() : ExtractedFolder());

            catalog.Objects.Should().HaveCount(2);
            var unit = catalog.Units.Should().ContainSingle().Subject;
            unit.Label.Should().Be("Тестовый воин (TST)");
            catalog.Objects.Single(o => !o.IsUnit).Descriptor.FileName.Should().Be("DEPO");

            unit.States.Select(s => s.Code).Should().Equal("STAND", "MOTION_L+MOTION_R", "MOTION_L", "MOTION_R", "MOTION_LB", "ATTACK", "DEATH", "DEATHLIE1");

            var walk = unit.States[1];
            walk.Label.Should().Be("Walk (MOTION_L+MOTION_R)");
            walk.Directions.Should().Be(9);
            walk.Frames.Should().Equal((1, 0), (1, 1), (1, 2), (1, 3));
            unit.States[4].Label.Should().Be("Walk backwards, left step (MOTION_LB)");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LoadsSpritesByName(bool archive)
        {
            using var catalog = GameCatalog.Load(archive ? Archive() : ExtractedFolder());

            catalog.Sprite("tstg").Frames.Should().HaveCount(36);
            catalog.Sprite("TSTS").Frames.Should().HaveCount(9);
            catalog.Sprite("missing").Should().BeNull();
            catalog.Sprite("tstg").Should().BeSameAs(catalog.Sprite("tstg"), "sprites are loaded once");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MatchesSoundsWithUnits(bool archive)
        {
            using var catalog = GameCatalog.Load(archive ? Archive() : ExtractedFolder());

            catalog.Sounds.Sounds.Select(sound => sound.Name).Should().Equal("STEP", "ALARM");
            catalog.Sounds.Find("alarm").Should().BeEquivalentTo(new GameSound("ALARM", new[] { @"Sound\Alarm.wav" }, true));

            var unit = catalog.Units.Single();
            unit.Descriptor.Sounds.Should().Equal(
                new GpDescriptorSound("MOTION_L", 1, "STEP"),
                new GpDescriptorSound("MOTION_R", 0, "STEP"),
                new GpDescriptorSound("ATTACK", 1, "ALARM"),
                new GpDescriptorSound("DEATH", 2, "ALARM"),
                new GpDescriptorSound("DEATH", 3, "GONE"));

            // GONE is not in the sound list.
            var sounds = catalog.SoundsOf(unit);
            sounds.Should().HaveCount(4);
            var sound = sounds[0];
            (sound.Animation, sound.Frame).Should().Be(("MOTION_L", 1));
            sound.Sound.Files.Should().Equal(@"Sound\Step1.wav", @"Sound\Step2.wav");
            sound.Sound.Danger.Should().BeFalse();

            // In the joined walk, the right step's frames follow the two of the left step.
            var walk = unit.States.Single(state => state.Code == "MOTION_L+MOTION_R");
            catalog.SoundsOf(unit, walk).Select(s => s.Frame).Should().Equal(1, 2);
            catalog.SoundsOf(unit, unit.States.Single(state => state.Code == "MOTION_R")).Select(s => s.Frame).Should().Equal(0);
            catalog.SoundsOf(unit, unit.States.Single(state => state.Code == "STAND")).Should().BeEmpty();
        }

        [Test]
        public void LoadsRlcSpritesByName()
        {
            File.WriteAllBytes(Path.Combine(_folder, "OLD.RLC"), GpTestFiles.RlcOfDots(frames: 9, x: 20, y: 10, color: 5));
            using var catalog = GameCatalog.Load(ExtractedFolder());

            catalog.RlcSprite("old").Frames.Should().HaveCount(9);
            catalog.Sprite("old").Should().BeNull("it is not a GP sprite");
            catalog.RlcSprite("tsts").Should().BeNull();
        }

        [Test]
        public void FindPrefersFilesInTheTopFolder()
        {
            Directory.CreateDirectory(Path.Combine(_folder, "SUB"));
            File.WriteAllText(Path.Combine(_folder, "SUB", "A.GP"), "sub");
            File.WriteAllText(Path.Combine(_folder, "a.gp"), "top");
            File.WriteAllText(Path.Combine(_folder, "SUB", "B.GP"), "only");

            using var files = GameFiles.Open(_folder);

            files.Find("A", ".GP").Should().Be("a.gp");
            files.Find("b", ".gp").Should().Be("SUB\\B.GP");
            files.Find("c", ".gp").Should().BeNull();
            Encoding.ASCII.GetString(files.Read(files.Find("b", ".gp"))).Should().Be("only");
        }
    }
}
