using NUnit.Framework;

namespace RoomOrchestra.Tests
{
    /// <summary>Toadstools and flowers grow out of nothing at their own pace and ease into the beat.</summary>
    public class SproutSyncTests
    {
        [Test]
        public void EachSproutHasItsOwnPaceWithinLimits()
        {
            Assert.AreEqual(0f, SproutSync.StartDelay(0f));
            Assert.AreEqual(SproutSync.MaxStartDelay, SproutSync.StartDelay(1f), 1e-6f);
            Assert.AreEqual(SproutSync.MinGrowSeconds, SproutSync.GrowSeconds(0f), 1e-6f);
            Assert.AreEqual(SproutSync.MaxGrowSeconds, SproutSync.GrowSeconds(1f), 1e-6f);
            Assert.AreEqual(-SproutSync.MaxOffsetBeats, SproutSync.InitialOffsetBeats(0f), 1e-6f);
            Assert.AreEqual(0f, SproutSync.InitialOffsetBeats(0.5f), 1e-6f, "some start right on the beat");
            Assert.AreEqual(SproutSync.MaxOffsetBeats, SproutSync.InitialOffsetBeats(1f), 1e-6f);
            Assert.Less(SproutSync.HiddenScale * 0.36f, 0.005f, "waiting, even the biggest sprout is under 5 mm");
        }

        [Test]
        public void ItGrowsFromNothingWithASpringyFinish()
        {
            Assert.AreEqual(0f, SproutSync.Size(0.2f, 0.3f, 1f), "waits out its start delay");
            Assert.AreEqual(1f, SproutSync.Size(2f, 0.3f, 1f), "then full size");
            float peak = 0f;
            for (float t = 0.3f; t < 1.3f; t += 0.01f) peak = System.Math.Max(peak, SproutSync.Size(t, 0.3f, 1f));
            Assert.Greater(peak, 1f, "pops a little past full before settling");
            Assert.Less(peak, 1.1f);
        }

        [Test]
        public void ItStartsOffTheBeatAndFallsIntoStepWithinAFewSeconds()
        {
            Assert.AreEqual(0.5f, SproutSync.OffsetBeats(0.5f, -1f), "off the beat while growing");
            Assert.AreEqual(0.5f, SproutSync.OffsetBeats(0.5f, 0f));
            Assert.Greater(SproutSync.OffsetBeats(0.5f, 1f), 0.15f, "still visibly off a second later");
            Assert.Less(SproutSync.OffsetBeats(0.5f, SproutSync.SettleSeconds), 0.02f, "in step after a few seconds");
            Assert.Less(SproutSync.OffsetBeats(-0.5f, 2f), 0f, "eases in from either side");
        }
    }
}
