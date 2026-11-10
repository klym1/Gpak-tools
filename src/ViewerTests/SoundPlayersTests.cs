using System;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Viewer;

namespace ViewerTests
{
    public class SoundPlayersTests
    {
        [Test]
        public void PicksTheSystemsPlayerCommand()
        {
            Assert.That(SoundPlayers.CommandFor(OSPlatform.OSX, command => true), Is.EqualTo("afplay"));
            Assert.That(SoundPlayers.CommandFor(OSPlatform.Linux, command => true), Is.EqualTo("paplay"));
            Assert.That(SoundPlayers.CommandFor(OSPlatform.Linux, command => command == "aplay"), Is.EqualTo("aplay"), "without PulseAudio");
            Assert.That(SoundPlayers.CommandFor(OSPlatform.Linux, command => false), Is.Null, "no player installed");
            Assert.That(SoundPlayers.CommandFor(OSPlatform.Windows, command => true), Is.Null, "Windows has its own interface");
        }

        [Test]
        public void StaysSilentWhenThePlayerCannotBeRun()
        {
            using var player = new CommandSoundPlayer("no-such-sound-player-" + Guid.NewGuid());

            Assert.DoesNotThrow(() => player.Play("a.wav"));
            Assert.DoesNotThrow(() => player.Play("b.wav"));
        }

        [Test]
        public void GivesAPlayerOnEverySystem()
        {
            Assert.That(SoundPlayers.ForThisSystem(), Is.Not.Null);
        }
    }
}
