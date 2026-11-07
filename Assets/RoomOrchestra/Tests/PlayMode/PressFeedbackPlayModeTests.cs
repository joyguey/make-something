using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>A press that lands jiggles the button, then leaves it exactly as it was (George, 2026-11-03).</summary>
    public class PressFeedbackPlayModeTests
    {
        GameObject m_Root;

        [SetUp] public void SetUp() => m_Root = new GameObject("PressFeedbackTest");

        [TearDown]
        public void TearDown()
        {
            PressJiggle.ReducedMotion = false;
            ComfortSettings.SetPosture(PlayPosture.Seated);
            Object.Destroy(m_Root);
        }

        GameObject Button()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(m_Root.transform, false);
            go.transform.SetLocalPositionAndRotation(new Vector3(0.2f, 1f, 1f), Quaternion.Euler(10f, 20f, 0f));
            go.transform.localScale = new Vector3(0.1f, 0.05f, 0.02f);
            go.AddComponent<HeightButton>();
            return go;
        }

        [UnityTest]
        public IEnumerator PressedButtonJigglesAndComesBackExactly()
        {
            var go = Button();
            var rotation = go.transform.localRotation;
            var scale = go.transform.localScale;
            yield return null;
            Assert.IsNull(go.GetComponent<PressJiggle>(), "nothing added before a press");

            Assert.IsTrue(InstrumentInputRouter.Activate(go));
            var jiggle = go.GetComponent<PressJiggle>();
            Assert.IsNotNull(jiggle);
            Assert.IsTrue(jiggle.Jiggling);
            float maxAngle = 0f, maxScale = 0f;
            float until = Time.unscaledTime + PressJiggleCurve.Duration + 0.2f;
            while (Time.unscaledTime < until)
            {
                yield return null;
                maxAngle = Mathf.Max(maxAngle, Quaternion.Angle(rotation, go.transform.localRotation));
                maxScale = Mathf.Max(maxScale, go.transform.localScale.x);
            }
            Assert.Greater(maxAngle, 1f, "it wobbles");
            Assert.Greater(maxScale, scale.x * 1.01f, "it swells");
            Assert.IsFalse(jiggle.Jiggling);
            Assert.AreEqual(0f, Quaternion.Angle(rotation, go.transform.localRotation), 1e-3f, "back to its pose");
            Assert.AreEqual(scale, go.transform.localScale, "back to its size");
        }

        [UnityTest]
        public IEnumerator ReducedMotionFlashesInsteadOfMoving()
        {
            PressJiggle.ReducedMotion = true;
            var go = Button();
            var rotation = go.transform.localRotation;
            yield return null;
            Assert.IsTrue(InstrumentInputRouter.Activate(go));
            Assert.IsNull(go.GetComponent<PressJiggle>(), "no wobble");
            Assert.IsTrue(go.GetComponent<ColorFlash>().IsFlashing, "a flash instead");
            yield return null;
            Assert.AreEqual(0f, Quaternion.Angle(rotation, go.transform.localRotation), 1e-3f);
        }

        [UnityTest]
        public IEnumerator NothingJigglesWhenThePressIsRefused()
        {
            var go = new GameObject("NotAButton");
            go.transform.SetParent(m_Root.transform, false);
            yield return null;
            Assert.IsFalse(InstrumentInputRouter.Activate(go));
            Assert.IsNull(go.GetComponent<PressJiggle>());
        }
    }
}
