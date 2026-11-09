using System;
using NUnit.Framework;

namespace RoomOrchestra.Tests
{
    /// <summary>The opening's motion: the leaf swoops in from out of view and the characters hop up onto it.</summary>
    public class IntroPathTests
    {
        // The leaf's home and the seated eye, in the stage's seated layout (LittleEchoesSceneSetup).
        const float HomeX = -0.2f, HomeY = 0.49f, HomeZ = 1.17f;
        const float EyeY = 1.15f;

        [Test]
        public void TheLeafStartsOutOfViewAndSettlesExactlyHome()
        {
            var start = IntroPath.LeafAt(0f);
            float x = HomeX + start.x, z = HomeZ + start.z;
            double yaw = Math.Abs(Math.Atan2(x, z) * 180.0 / Math.PI);
            Assert.Greater(yaw, 55.0, "starts beyond the edge of a seated player's view, so the scene opens without it");

            var end = IntroPath.LeafAt(IntroPath.SwoopSeconds);
            Assert.AreEqual(0f, end.x, 1e-5f);
            Assert.AreEqual(0f, end.y, 1e-5f);
            Assert.AreEqual(0f, end.z, 1e-5f);
            Assert.AreEqual(0f, end.pitch, 1e-3f);
            Assert.AreEqual(0f, end.yaw, 1e-3f);
            Assert.AreEqual(0f, end.roll, 1e-3f);
            Assert.AreEqual(end, IntroPath.LeafAt(IntroPath.SwoopSeconds + 5f), "stays home");
        }

        [Test]
        public void TheSwoopStaysAboveTheFloorAndClearOfTheFace()
        {
            bool rocked = false;
            for (float t = 0f; t <= IntroPath.SwoopSeconds; t += 0.02f)
            {
                var p = IntroPath.LeafAt(t);
                Assert.Greater(HomeY + p.y, 0.3f, $"above the floor at {t:F2} s");
                float dx = HomeX + p.x, dy = HomeY + p.y - EyeY, dz = HomeZ + p.z;
                Assert.Greater(Math.Sqrt(dx * dx + dy * dy + dz * dz), 0.8, $"never close to the face at {t:F2} s");
                if (Math.Abs(p.roll) > 10f) rocked = true;
            }
            Assert.IsTrue(rocked, "rocks side to side like a falling leaf");
        }

        [Test]
        public void EachCharacterBouncesTwiceThenLeapsOntoTheLeaf()
        {
            Assert.AreEqual((0f, 0f, false), IntroPath.HopAt(0f), "waits on the floor");
            float floorTime = IntroPath.BounceSeconds[0] + IntroPath.BounceSeconds[1];
            Assert.AreEqual(0f, IntroPath.HopAt(IntroPath.BounceSeconds[0] * 0.5f).blend, "the first bounces stay on the floor spot");
            Assert.Greater(IntroPath.HopAt(IntroPath.BounceSeconds[0] * 0.5f).lift, 0.04f, "but leave the floor");
            var leap = IntroPath.HopAt(floorTime + IntroPath.BounceSeconds[2] * 0.5f);
            Assert.AreEqual(0.5f, leap.blend, 0.01f, "halfway across in the leap");
            Assert.Greater(leap.lift, 0.2f, "arching up and over onto the leaf");
            float total = floorTime + IntroPath.BounceSeconds[2];
            Assert.AreEqual((1f, 0f, true), IntroPath.HopAt(total + 0.01f), "landed");
            Assert.AreEqual(-1, IntroPath.BounceIndex(total + 0.01f));
            Assert.Less(IntroPath.TotalSeconds(3), 5f, "a short opening");
        }
    }
}
