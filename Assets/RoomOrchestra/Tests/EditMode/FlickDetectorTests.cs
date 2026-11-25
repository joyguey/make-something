using NUnit.Framework;
using UnityEngine;

namespace RoomOrchestra.Tests
{
    /// <summary>Story mode's flick (owner, 2026-11-22: flick the friend above the open left palm with the right hand).</summary>
    public class FlickDetectorTests
    {
        const float Dt = 1f / 72f;

        [Test]
        public void AQuickMoveFromBesideTheFriendFiresOnceTowardTheMove()
        {
            var f = new FlickDetector(); var friend = new Vector3(0f, 1f, 0.4f);
            var hand = friend + new Vector3(0.1f, 0f, 0f); float t = 0f; int fired = 0; Vector3 last = default;
            for (int i = 0; i < 10; i++) { f.Step(hand, friend, t, Dt, out _); t += Dt; }  // resting beside it
            for (int i = 0; i < 12; i++)  // a quick flick forward (2 m/s)
            {
                hand += Vector3.forward * 2f * Dt;
                if (f.Step(hand, friend, t, Dt, out var d)) { fired++; last = d; }
                t += Dt;
            }
            Assert.AreEqual(1, fired, "one flick, once");
            Assert.Greater(Vector3.Dot(last, Vector3.forward), 0.9f, "toward the move");
        }

        [Test]
        public void SlowMovesAndMovesFarFromTheFriendDoNotFire()
        {
            var f = new FlickDetector(); var friend = new Vector3(0f, 1f, 0.4f);
            var hand = friend + new Vector3(0.1f, 0f, 0f); float t = 0f;
            for (int i = 0; i < 40; i++) { hand += Vector3.forward * 0.4f * Dt; Assert.IsFalse(f.Step(hand, friend, t, Dt, out _), "slow"); t += Dt; }
            hand = friend + new Vector3(0.8f, 0f, 0f);
            for (int i = 0; i < 40; i++) { f.Step(hand, friend, t, Dt, out _); t += Dt; }
            for (int i = 0; i < 12; i++) { hand += Vector3.forward * 2.5f * Dt; Assert.IsFalse(f.Step(hand, friend, t, Dt, out _), "far from the friend"); t += Dt; }
        }

        [Test]
        public void AimAssistTurnsTowardANearbyTargetOnly()
        {
            var to = FlickDetector.Assist(Vector3.zero, Vector3.forward, new[] { new Vector3(0.2f, 0f, 2f) }, 22f);
            Assert.Greater(Vector3.Angle(Vector3.forward, to), 1f, "pulled toward the target");
            var far = FlickDetector.Assist(Vector3.zero, Vector3.forward, new[] { new Vector3(2f, 0f, 0.5f) }, 22f);
            Assert.AreEqual(0f, Vector3.Angle(Vector3.forward, far), 1e-3f, "a target far off-line is ignored");
        }
    }
}
