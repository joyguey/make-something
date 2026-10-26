using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>Character life (face + jelly body) with real transforms; primitive stand-ins for the parts.</summary>
    public class CharacterFacePlayModeTests
    {
        GameObject m_Root;
        Transform m_Visual, m_EyeL, m_EyeR, m_Smile, m_Oh;
        static readonly Vector3 OhParked = new Vector3(0f, -0.2f, 0.25f - 0.41f);

        [UnitySetUp]
        public IEnumerator Build()
        {
            // Life starts a few seconds after load (PICO Spatial startup pose race); wait that out once.
            while (Time.timeSinceLevelLoad < JellyBody.StartDelaySeconds + 0.1f) yield return null;
            m_Root = new GameObject("Character");
            m_Visual = new GameObject("Visual").transform;
            m_Visual.SetParent(m_Root.transform, false);
            Transform Part(string name, Vector3 at)
            {
                var t = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
                t.name = name;
                Object.Destroy(t.GetComponent<Collider>());
                t.SetParent(m_Visual, false);
                t.localPosition = at;
                t.localScale = new Vector3(0.1f, 0.15f, 0.06f);
                return t;
            }
            m_EyeL = Part("EyeL", new Vector3(-0.15f, 0.07f, -0.36f));
            m_EyeR = Part("EyeR", new Vector3(0.15f, 0.07f, -0.36f));
            m_Smile = Part("Smile", new Vector3(0f, -0.18f, -0.41f));
            m_Oh = Part("Mouth_O", OhParked);
        }

        [TearDown]
        public void TearDown()
        {
            CharacterFace.ReducedMotion = false;
            JellyBody.ReducedMotion = false;
            Object.Destroy(m_Root);
        }

        CharacterFace Face(bool withOh = true)
        {
            var face = m_Root.AddComponent<CharacterFace>();
            face.Configure(m_EyeL, m_EyeR, m_Smile, withOh ? m_Oh : null);
            return face;
        }

        JellyBody Jelly(OrbType type = OrbType.Bop, bool awake = true)
        {
            var jelly = m_Root.AddComponent<JellyBody>();
            jelly.Configure(m_Visual, type, awake);
            return jelly;
        }

        [UnityTest]
        public IEnumerator CuriousSwapsSmileForOhWithWideEyesThenReturns()
        {
            var face = Face();
            var smileHome = m_Smile.localPosition;
            var eye = m_EyeL.localScale;
            face.Show(Expression.Curious);
            Assert.Less((m_Oh.localPosition - (OhParked - new Vector3(0f, 0f, 0.25f))).magnitude, 1e-5f, "oh comes forward to the face");
            Assert.Greater(m_Smile.localPosition.z, smileHome.z + 0.2f, "smile sinks into the body");
            Assert.Greater(m_EyeL.localScale.x, eye.x * 1.4f, "wide eyes");
            yield return new WaitForSeconds(CharacterFace.HoldSeconds + 0.1f);
            Assert.AreEqual(Expression.Idle, face.Current);
            Assert.AreEqual(smileHome, m_Smile.localPosition);
            Assert.AreEqual(OhParked, m_Oh.localPosition, "oh parked again");
        }

        [UnityTest]
        public IEnumerator SiblingsGlanceSidewaysTowardEachOther()
        {
            var face = Face();
            var left = m_EyeL.localPosition;
            face.Show(Expression.Sibling, +1f);
            Assert.Greater(m_EyeL.localPosition.x, left.x + 0.05f, "looks right");
            face.Show(Expression.Sibling, -1f);
            Assert.Less(m_EyeL.localPosition.x, left.x - 0.05f, "looks left");
            Assert.AreEqual(m_EyeL.localPosition.y, left.y, 1e-6f, "only sideways");
            yield return null;
        }

        [UnityTest]
        public IEnumerator IdleCharactersBlinkAndBreatheOnTheirOwn()
        {
            Face();
            Jelly(OrbType.Flutter);
            var eye = m_EyeL.localScale.y;
            float minEye = eye, minY = 1f, maxY = 1f;
            for (float end = Time.time + 6f; Time.time < end;)
            {
                yield return null;
                minEye = Mathf.Min(minEye, m_EyeL.localScale.y);
                minY = Mathf.Min(minY, m_Visual.localScale.y);
                maxY = Mathf.Max(maxY, m_Visual.localScale.y);
            }
            Assert.Less(minEye, eye * 0.5f, "blinked at least once");
            Assert.Greater(maxY - minY, 0.05f, "breathes visibly");
        }

        [UnityTest]
        public IEnumerator DraggedJellyLeansAndWobblesThenSettles()
        {
            var jelly = Jelly(OrbType.Bop);
            yield return null;
            // Drag sideways at 1 m/s for a third of a second, then stop dead.
            for (float end = Time.time + 0.33f; Time.time < end;)
            {
                m_Root.transform.position += Vector3.right * Time.deltaTime;
                yield return null;
            }
            Assert.Greater(Mathf.Abs(jelly.Lean.y), 4f, "leans while dragged");
            float first = Mathf.Sign(jelly.Lean.y);
            bool swungBack = false;
            for (float end = Time.time + 0.6f; Time.time < end;)
            {
                yield return null;
                if (Mathf.Sign(jelly.Lean.y) != first && Mathf.Abs(jelly.Lean.y) > 1f) swungBack = true;
            }
            Assert.IsTrue(swungBack, "wobbles past upright when the drag stops (jelly, not a stiff tilt)");
            yield return new WaitForSeconds(2.5f);
            Assert.Less(Mathf.Abs(jelly.Lean.y), 1f, "settles");
        }

        [UnityTest]
        public IEnumerator HopLiftsTheCharacterAndLandsWhereItStarted()
        {
            var jelly = Jelly(OrbType.Flutter);
            yield return null;
            jelly.Hop();
            float peak = 0f;
            for (float end = Time.time + CharacterMotion.For(OrbType.Flutter).HopSeconds; Time.time < end;)
            {
                yield return null;
                peak = Mathf.Max(peak, m_Visual.localPosition.y);
            }
            Assert.Greater(peak, 0.3f, "a hop you can see (Flutter: ~0.6 body heights)");
            yield return new WaitForSeconds(0.3f);
            Assert.Less(Mathf.Abs(m_Visual.localPosition.y), 0.08f, "back down (breathing aside)");
        }

        [UnityTest]
        public IEnumerator AsleepCharactersRestInTheirAuthoredPose()
        {
            var jelly = Jelly(OrbType.Boom, awake: true);
            jelly.Hop();
            yield return new WaitForSeconds(0.1f);
            jelly.SetAwake(false);
            Assert.AreEqual(Vector3.zero, m_Visual.localPosition);
            Assert.AreEqual(Vector3.one, m_Visual.localScale, "fits its hiding place again");
            m_Root.transform.position += Vector3.right; // carried away while asleep
            for (int i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(Vector3.one, m_Visual.localScale, "nothing moves while asleep");
            jelly.SetAwake(true);
            yield return null;
            Assert.Less(jelly.Lean.magnitude, 1f, "waking up somewhere new is not a fling");
        }

        [UnityTest]
        public IEnumerator ReducedMotionKeepsFacesButHoldsTheBodyStill()
        {
            CharacterFace.ReducedMotion = true;
            JellyBody.ReducedMotion = true;
            var face = Face();
            Jelly();
            face.Show(Expression.Curious);
            Assert.Greater(m_EyeL.localScale.x, 0.14f, "the face still changes");
            for (float end = Time.time + 1f; Time.time < end;)
            {
                m_Root.transform.position += Vector3.right * Time.deltaTime;
                yield return null;
                Assert.AreEqual(Vector3.one, m_Visual.localScale, "no breathing or wobble");
            }
        }
    }
}
