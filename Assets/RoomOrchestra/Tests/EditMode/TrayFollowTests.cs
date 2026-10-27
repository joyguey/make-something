using NUnit.Framework;

namespace RoomOrchestra.Tests
{
    /// <summary>The leaf pad's rules: lazy follow away from the seat (A) and the call (C).</summary>
    public class TrayFollowTests
    {
        const float Frame = 1f / 72f;

        [Test]
        public void StaysHomeWhileThePlayerSitsAndLooksAround()
        {
            var follow = new TrayFollow();
            for (int i = 0; i < 200; i++)
                Assert.IsFalse(follow.Update(0.3f * (float)System.Math.Sin(i * 0.1), 0.2f, i * 3f, Frame), "leaning and turning at the seat");
            Assert.AreEqual(TrayMode.Home, follow.Mode);
        }

        [Test]
        public void FollowsOnceThePlayerWalksAwayAndPutsTheControlsInFrontOfThem()
        {
            var follow = new TrayFollow();
            Assert.IsTrue(follow.Update(1.5f, 0.5f, 90f, Frame));
            Assert.AreEqual(TrayMode.Following, follow.Mode);
            Assert.AreEqual(1.5f, follow.Target.X, 1e-5f);
            Assert.AreEqual(0.5f, follow.Target.Z, 1e-5f);
            Assert.AreEqual(90f, follow.Target.YawDegrees, 1e-5f, "turned to face the way the player faces");
            Assert.IsFalse(follow.Called, "drifts, not a call");
        }

        [Test]
        public void SmallMovesAndTurnsDoNotJiggleIt()
        {
            var follow = new TrayFollow();
            follow.Update(2f, 0f, 0f, Frame);
            Assert.IsFalse(follow.Update(2.3f, 0.2f, 30f, Frame), "inside the dead zone");
            Assert.IsTrue(follow.Update(2.6f, 0f, 0f, Frame), "a real step");
            Assert.IsTrue(follow.Update(2.6f, 0f, 60f, Frame), "a real turn");
            Assert.IsTrue(follow.Update(2.6f, 0f, -170f, Frame) , "turning the long way round still counts (wraps at 180)");
        }

        [Test]
        public void GoesHomeAfterThePlayerIsBackAtTheSeatForAMoment()
        {
            var follow = new TrayFollow();
            follow.Update(2f, 0f, 0f, Frame);
            float t = 0f;
            while (t < TrayFollow.ReturnDwellSeconds - 0.1f)
            {
                follow.Update(0.1f, 0f, 0f, Frame);
                t += Frame;
            }
            Assert.AreEqual(TrayMode.Following, follow.Mode, "passing by the seat is not sitting down");
            for (int i = 0; i < 20; i++) follow.Update(0.1f, 0f, 0f, Frame);
            Assert.AreEqual(TrayMode.Home, follow.Mode);
            Assert.AreEqual(0f, follow.Target.X);
            Assert.AreEqual(0f, follow.Target.YawDegrees);
        }

        [Test]
        public void CallBringsItNowEvenInsideTheLeaveRadiusAndSendsItHomeFromTheSeat()
        {
            var follow = new TrayFollow();
            Assert.IsFalse(follow.Call(0.1f, 0.1f, 0f), "at the seat and already home: nothing to do");
            Assert.IsTrue(follow.Call(0.7f, -0.3f, 180f), "a step away, turned round: it comes");
            Assert.IsTrue(follow.Called);
            Assert.AreEqual(TrayMode.Following, follow.Mode);
            Assert.AreEqual(180f, follow.Target.YawDegrees, 1e-5f);
            Assert.IsTrue(follow.Call(0f, 0f, 0f), "called from the seat: home");
            Assert.AreEqual(TrayMode.Home, follow.Mode);
            Assert.IsTrue(follow.Called, "comes home quickly too");
        }

        [Test]
        public void ResetIsHome()
        {
            var follow = new TrayFollow();
            follow.Update(3f, 3f, 45f, Frame);
            follow.Reset();
            Assert.AreEqual(TrayMode.Home, follow.Mode);
            Assert.AreEqual(0f, follow.Target.X);
            Assert.AreEqual(0f, follow.Target.Z);
        }
    }
}
