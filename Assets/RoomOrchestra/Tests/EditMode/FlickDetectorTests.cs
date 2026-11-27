using NUnit.Framework;
using UnityEngine;

namespace RoomOrchestra.Tests
{
    /// <summary>
    /// Story mode's flick (owner, 2026-11-25: "Do you know what a flick should look like?"): the index finger loaded
    /// against the thumb (or curled) beside the friend, then snapping out fast relative to the hand.
    /// </summary>
    public class FlickDetectorTests
    {
        const float Dt = 1f / 72f;
        static readonly Vector3 Friend = new Vector3(0f, 1f, 0.4f);

        // a right hand: the palm, the index finger pointing forward 'ext' metres from it, the thumb tip just past 5 cm
        static FlickHand Hand(Vector3 palm, float ext) => new FlickHand
        {
            Palm = palm, Tip = palm + Vector3.forward * ext, Thumb = palm + Vector3.forward * 0.05f + Vector3.right * 0.01f, Friend = Friend,
        };

        static int Run(FlickDetector f, ref float t, Vector3 palm, float from, float to, int frames, ref Vector3 dir)
        {
            int fired = 0;
            for (int i = 1; i <= frames; i++)
            {
                if (f.Step(Hand(palm, Mathf.Lerp(from, to, i / (float)frames)), t, Dt, out var d)) { fired++; dir = d; }
                t += Dt;
            }
            return fired;
        }

        [Test]
        public void ALoadedFingerSnappingOutBesideTheFriendFiresOnceTheWayItSnapped()
        {
            var f = new FlickDetector(); float t = 0f; var dir = Vector3.zero;
            var palm = Friend + new Vector3(0.06f, 0f, -0.06f);
            Assert.AreEqual(0, Run(f, ref t, palm, 0.06f, 0.06f, 10, ref dir), "loaded and waiting");
            Assert.IsTrue(f.Near, "ready: the friend can glow");
            Assert.AreEqual(1, Run(f, ref t, palm, 0.06f, 0.12f, 4, ref dir), "the snap (about 1 m/s), once");
            Assert.Greater(Vector3.Dot(dir, Vector3.forward), 0.9f, "the way the finger snapped");
        }

        [Test]
        public void ASnapThatPeaksBeforeTheFingerLeavesTheThumbStillFires()
        {
            // headset 2026-11-26: real snaps of 0.7 to 1 m/s peaked while the finger still touched the thumb and were lost
            var f = new FlickDetector(); float t = 0f; var dir = Vector3.zero;
            var palm = Friend + new Vector3(0.06f, 0f, -0.06f);
            Run(f, ref t, palm, 0.05f, 0.05f, 10, ref dir);
            Assert.AreEqual(0, Run(f, ref t, palm, 0.05f, 0.08f, 2, ref dir), "fast, but still against the thumb");
            Assert.AreEqual(1, Run(f, ref t, palm, 0.08f, 0.09f, 6, ref dir), "leaving the thumb just after: the snap counts, once");
            Assert.Greater(Vector3.Dot(dir, Vector3.forward), 0.9f);
        }

        [Test]
        public void AFingerEasingOffTheThumbSlowlyIsNotAFlick()
        {
            var f = new FlickDetector(); float t = 0f; var dir = Vector3.zero;
            var palm = Friend + new Vector3(0.06f, 0f, -0.06f);
            Run(f, ref t, palm, 0.05f, 0.05f, 10, ref dir);
            Assert.AreEqual(0, Run(f, ref t, palm, 0.05f, 0.1f, 60, ref dir), "about 0.06 m/s: no flick");
        }

        [Test]
        public void AWholeHandSwipeIsNotAFlick()
        {
            var f = new FlickDetector(); float t = 0f;
            var palm = Friend + new Vector3(0.3f, 0f, -0.06f);
            for (int i = 0; i < 30; i++) // the finger held out straight, the whole hand sweeping past the friend fast
            {
                palm += Vector3.left * 2f * Dt;
                Assert.IsFalse(f.Step(Hand(palm, 0.11f), t, Dt, out _));
                t += Dt;
            }
            palm = Friend + new Vector3(0.3f, 0f, -0.06f);
            for (int i = 0; i < 30; i++) // loaded, but again the whole hand moves, not the finger
            {
                palm += Vector3.left * 2f * Dt;
                Assert.IsFalse(f.Step(Hand(palm, 0.06f), t, Dt, out _));
                t += Dt;
            }
        }

        [Test]
        public void FarFromTheFriendASnapDoesNotFire()
        {
            var f = new FlickDetector(); float t = 0f; var dir = Vector3.zero;
            var palm = Friend + new Vector3(0.5f, 0f, 0f);
            Run(f, ref t, palm, 0.06f, 0.06f, 10, ref dir);
            Assert.AreEqual(0, Run(f, ref t, palm, 0.06f, 0.12f, 4, ref dir));
        }

        [Test]
        public void AfterAFlickTheFingerMustLoadAgain()
        {
            // headset 2026-11-25: "the second ball is getting launched too soon"
            var f = new FlickDetector(); float t = 0f; var dir = Vector3.zero;
            var palm = Friend + new Vector3(0.06f, 0f, -0.06f);
            Run(f, ref t, palm, 0.06f, 0.06f, 10, ref dir);
            Assert.AreEqual(1, Run(f, ref t, palm, 0.06f, 0.12f, 4, ref dir));
            Assert.IsFalse(f.Armed);
            int more = 0;
            for (int k = 0; k < 6; k++) more += Run(f, ref t, palm, 0.12f, 0.09f, 3, ref dir) + Run(f, ref t, palm, 0.09f, 0.13f, 3, ref dir);
            Assert.AreEqual(0, more, "an open finger wiggling after the flick fires nothing");
            Run(f, ref t, palm, 0.12f, 0.06f, 8, ref dir); // curled again: loaded
            Run(f, ref t, palm, 0.06f, 0.06f, 6, ref dir);
            Assert.IsTrue(f.Armed);
            Assert.AreEqual(1, Run(f, ref t, palm, 0.06f, 0.12f, 4, ref dir), "the next flick");
        }

        [Test]
        public void AimAssistTurnsTowardANearbyTargetOnly()
        {
            var to = FlickDetector.Assist(Vector3.zero, Vector3.forward, new[] { new Vector3(0.2f, 0f, 2f) }, 22f);
            Assert.Greater(Vector3.Angle(Vector3.forward, to), 1f, "pulled toward the target");
            var far = FlickDetector.Assist(Vector3.zero, Vector3.forward, new[] { new Vector3(2f, 0f, 0.5f) }, 22f);
            Assert.AreEqual(0f, Vector3.Angle(Vector3.forward, far), 1e-3f, "a target far off-line is ignored");
        }
        [Test]
        public void ATrackingJumpIsNotAFlick()
        {
            var f = new FlickDetector(); float t = 0f; var dir = Vector3.zero;
            var palm = Friend + new Vector3(0.06f, 0f, -0.06f);
            Run(f, ref t, palm, 0.06f, 0.06f, 10, ref dir);
            Assert.IsFalse(f.Step(Hand(palm, 0.36f), t, Dt, out _), "0.3 m in a frame");
            t += Dt;
            Assert.IsFalse(f.Step(Hand(palm, 0.36f), t, Dt, out _), "and no leftover speed after it");
        }

        [Test]
        public void ANearMissReportsItsFastestSnap()
        {
            var f = new FlickDetector(); float t = 0f; var dir = Vector3.zero;
            var palm = Friend + new Vector3(0.06f, 0f, -0.06f);
            Run(f, ref t, palm, 0.06f, 0.06f, 10, ref dir);
            Assert.AreEqual(0, Run(f, ref t, palm, 0.06f, 0.12f, 14, ref dir), "too slow to be a flick (0.3 m/s)");
            Run(f, ref t, palm, 0.12f, 0.12f, 30, ref dir);
            Assert.IsTrue(f.TakeMiss(out var peak), "loaded beside it, no flick");
            Assert.AreEqual(0.3f, peak, 0.08f);
            Assert.IsFalse(f.TakeMiss(out _), "reported once");
        }

        [Test]
        public void TurningThePalmOverAndBackIsOneFlip()
        {
            var p = new PalmFlip(); float t = 0f;
            Assert.IsFalse(p.Step(Vector3.up, t));
            t += 0.3f; Assert.IsFalse(p.Step(Vector3.down, t), "over");
            t += 0.4f; Assert.IsTrue(p.Step(Vector3.up, t), "and back");
            t += 0.1f; Assert.IsFalse(p.Step(new Vector3(1f, 0.1f, 0f), t), "sideways is neither");
            t += 0.3f; Assert.IsFalse(p.Step(Vector3.down, t));
            t += 3f; p.Settle(t);
            Assert.IsFalse(p.Step(Vector3.up, t + 0.1f), "too slow: left over, then turned back is not a flip");
        }
        [Test]
        public void StoryEffectsBlendThePicturesAlphaSoTheRealRoomNeverCoversTheLevel()
        {
            // PICO shows the real room where the picture's alpha is 0 (headset 2026-11-25: the clear mist round the head
            // overwrote it and the level was only seen through the hand's outline)
            var m = StoryArt.Glow(new Color(0.4f, 0.4f, 0.4f, 0f), transparent: true);
            Assert.AreEqual((float)UnityEngine.Rendering.BlendMode.One, m.GetFloat("_SrcBlendAlpha"));
            Assert.AreEqual((float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha, m.GetFloat("_DstBlendAlpha"), "an almost clear effect keeps what is under it");
            Object.DestroyImmediate(m);
        }
    }
}
