using NUnit.Framework;

namespace RoomOrchestra.Tests
{
    /// <summary>Controller vibration: gentle, short, heavier balls felt more, and off when the player turns it off.</summary>
    public class HapticsTests
    {
        [SetUp] public void On() => Haptics.Enabled = true;
        [TearDown] public void Restore() => Haptics.Enabled = true;

        [Test]
        public void EveryPulseIsShortAndNeverFullStrength()
        {
            foreach (var (strength, ms) in new[]
                     {
                         (Haptics.PressStrength, Haptics.PressMs), (Haptics.GrabStrength, Haptics.GrabMs), (Haptics.ThrowStrength, Haptics.ThrowMs),
                         Haptics.HitFor(OrbType.Flutter), Haptics.HitFor(OrbType.Bop), Haptics.HitFor(OrbType.Boom),
                     })
            {
                Assert.That(strength, Is.InRange(0.1f, 0.7f));
                Assert.That(ms, Is.InRange(10, 80));
            }
        }

        [Test]
        public void HeavierBallsAreFeltMore()
        {
            Assert.Less(Haptics.HitFor(OrbType.Flutter).strength, Haptics.HitFor(OrbType.Bop).strength);
            Assert.Less(Haptics.HitFor(OrbType.Bop).strength, Haptics.HitFor(OrbType.Boom).strength);
        }

        [Test]
        public void AHitIsFeltInTheHandThatPlayedAndNothingWhenTurnedOff()
        {
            Haptics.Pulse(0, Haptics.ThrowStrength, Haptics.ThrowMs);
            Haptics.Hit(OrbType.Boom);
            Assert.AreEqual(0, Haptics.Last.hand, "the left hand threw, the left hand feels it land");
            Assert.AreEqual(Haptics.HitFor(OrbType.Boom).strength, Haptics.Last.strength);
            int before = Haptics.Count;
            Haptics.Enabled = false;
            Haptics.Pulse(1, 0.5f, 30);
            Assert.AreEqual(before, Haptics.Count);
        }
    }
}
