using NUnit.Framework;

namespace RoomOrchestra.Tests
{
    /// <summary>The jiggle a press gives (George, 2026-11-03): short, gentle, and it always ends where it began.</summary>
    public class PressFeedbackTests
    {
        [Test]
        public void JiggleIsShortGentleAndSettles()
        {
            Assert.LessOrEqual(PressJiggleCurve.Duration, 0.4f, "over before the next tap");
            Assert.AreEqual(0f, PressJiggleCurve.Angle(0f));
            Assert.AreEqual(0f, PressJiggleCurve.Swell(0f));
            Assert.AreEqual(0f, PressJiggleCurve.Angle(PressJiggleCurve.Duration));
            Assert.AreEqual(0f, PressJiggleCurve.Swell(PressJiggleCurve.Duration));
            float maxAngle = 0f, maxSwell = 0f;
            for (float t = 0f; t < PressJiggleCurve.Duration; t += 0.002f)
            {
                maxAngle = System.Math.Max(maxAngle, System.Math.Abs(PressJiggleCurve.Angle(t)));
                maxSwell = System.Math.Max(maxSwell, PressJiggleCurve.Swell(t));
                Assert.GreaterOrEqual(PressJiggleCurve.Swell(t), 0f, "it only ever swells, never shrinks");
            }
            Assert.Greater(maxAngle, 2f, "big enough to notice");
            Assert.LessOrEqual(maxAngle, PressJiggleCurve.MaxAngle);
            Assert.Greater(maxSwell, 0.02f);
            Assert.LessOrEqual(maxSwell, PressJiggleCurve.MaxSwell);
            Assert.Less(System.Math.Abs(PressJiggleCurve.Angle(PressJiggleCurve.Duration * 0.95f)), 0.2f, "settles smoothly");
        }
    }
}
