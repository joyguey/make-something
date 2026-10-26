using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>Character expressions with real transforms (primitive stand-ins for the face parts).</summary>
    public class CharacterFacePlayModeTests
    {
        GameObject m_Root;
        Transform m_Visual, m_EyeL, m_EyeR, m_Smile, m_Oh;
        static readonly Vector3 OhParked = new Vector3(0f, -0.2f, 0.25f - 0.41f);

        [SetUp]
        public void Build()
        {
            m_Root = new GameObject("Character");
            m_Visual = new GameObject("Visual").transform;
            m_Visual.SetParent(m_Root.transform, false);
            Transform Part(string name, Vector3 at)
            {
                var t = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
                t.name = name;
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
            Object.Destroy(m_Root);
        }

        CharacterFace Face(bool withOh = true)
        {
            var face = m_Root.AddComponent<CharacterFace>();
            face.Configure(m_Visual, m_EyeL, m_EyeR, m_Smile, withOh ? m_Oh : null);
            return face;
        }

        [UnityTest]
        public IEnumerator NothingMovesUntilTheFirstReaction()
        {
            Face();
            var eye = m_EyeL.localScale;
            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreEqual(eye, m_EyeL.localScale, "authored pose kept at startup (PICO Spatial)");
            Assert.AreEqual(OhParked, m_Oh.localPosition);
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
            Assert.Greater(m_EyeL.localScale.x, eye.x * 1.2f, "wide eyes");
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
            Assert.Greater(m_EyeL.localPosition.x, left.x + 0.03f, "looks right");
            face.Show(Expression.Sibling, -1f);
            Assert.Less(m_EyeL.localPosition.x, left.x - 0.03f, "looks left");
            Assert.AreEqual(m_EyeL.localPosition.y, left.y, 1e-6f, "only sideways");
            yield return null;
        }

        [UnityTest]
        public IEnumerator BounceSquashesTheVisualAndSettles()
        {
            var face = Face(withOh: false);
            face.Show(Expression.Curious); // without an oh mouth, surprise is the eyes alone
            Assert.AreEqual(new Vector3(0f, -0.18f, -0.41f), m_Smile.localPosition, "smile stays when there is no oh");
            face.Bounce();
            yield return new WaitForSeconds(0.15f);
            Assert.Less(m_Visual.localScale.y, 0.97f, "squashed mid-bounce");
            Assert.Greater(m_Visual.localScale.x, 1.03f);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(Vector3.one, m_Visual.localScale, "back to shape");
        }

        [UnityTest]
        public IEnumerator ReducedMotionKeepsFacesButNoBounceOrBlink()
        {
            CharacterFace.ReducedMotion = true;
            var face = Face();
            var eye = m_EyeL.localScale;
            face.Show(Expression.Curious);
            Assert.Greater(m_EyeL.localScale.x, eye.x * 1.2f, "the face still changes");
            face.Bounce();
            float minEye = float.MaxValue;
            float end = Time.time + CharacterFace.HoldSeconds + 8f; // long enough for several blinks
            while (Time.time < end)
            {
                Assert.AreEqual(Vector3.one, m_Visual.localScale, "no bounce");
                if (face.Current == Expression.Idle) minEye = Mathf.Min(minEye, m_EyeL.localScale.y);
                yield return null;
            }
            Assert.AreEqual(eye.y, minEye, 1e-6f, "no blink");
        }
    }
}
