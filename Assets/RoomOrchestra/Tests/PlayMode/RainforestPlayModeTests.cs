using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>Rainforest life with real transforms: butterflies fly and trail sparkles; sparkles flare with their instrument.</summary>
    public class RainforestPlayModeTests
    {
        GameObject m_Root;

        [UnitySetUp]
        public IEnumerator Wait()
        {
            while (Time.timeSinceLevelLoad < JellyBody.StartDelaySeconds + 0.1f) yield return null;
            m_Root = new GameObject("Stage");
        }

        [TearDown]
        public void TearDown()
        {
            Butterfly.ReducedMotion = false;
            SparkleTwinkle.ReducedMotion = false;
            MusicalHitPipeline.Reset();
            Object.Destroy(m_Root);
        }

        Transform Child(string name, Transform parent = null)
        {
            var t = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            t.name = name;
            Object.Destroy(t.GetComponent<Collider>());
            t.SetParent(parent != null ? parent : m_Root.transform, false);
            t.localScale = Vector3.one * 0.02f;
            return t;
        }

        Butterfly MakeButterfly()
        {
            var body = new GameObject("Butterfly").transform;
            body.SetParent(m_Root.transform, false);
            var flier = body.gameObject.AddComponent<Butterfly>();
            var trail = new Transform[8];
            for (int i = 0; i < trail.Length; i++) trail[i] = Child($"Trail{i}");
            flier.Configure(Child("WingL", body), Child("WingR", body), trail, new Vector3(0f, 1.5f, 2.3f), new Vector3(1.4f, 0.25f, 0.45f), 10f, 0f);
            return flier;
        }

        [UnityTest]
        public IEnumerator ButterfliesFlyFlapAndLeaveASparkleTrail()
        {
            var flier = MakeButterfly();
            yield return null;
            var start = flier.transform.position;
            float minWing = 999f, maxWing = -999f;
            for (float end = Time.time + 1.5f; Time.time < end;)
            {
                yield return null;
                float lift = Mathf.DeltaAngle(0f, flier.WingL.localEulerAngles.z);
                minWing = Mathf.Min(minWing, lift);
                maxWing = Mathf.Max(maxWing, lift);
            }
            Assert.Greater((flier.transform.position - start).magnitude, 0.2f, "flies along its loop");
            Assert.Greater(maxWing - minWing, 20f, "flaps");
            Assert.Less(maxWing, 0f, "the left wing always stays up (a V), never droops below flat");
            var now = flier.transform.position;
            Assert.Greater(Vector3.Distance(flier.Trail[7].position, now), Vector3.Distance(flier.Trail[0].position, now),
                "the trail stretches out behind it, oldest sparkle furthest");
            Assert.Less(flier.Trail[7].localScale.x, flier.Trail[0].localScale.x, "older sparkles are smaller");
        }

        [UnityTest]
        public IEnumerator SparklesFlareOnlyForTheirOwnInstrument()
        {
            InstrumentTarget Target(InstrumentType type)
            {
                var go = new GameObject(type.ToString());
                go.transform.SetParent(m_Root.transform, false);
                go.AddComponent<BoxCollider>();
                go.AddComponent<AudioSource>();
                var t = go.AddComponent<InstrumentTarget>();
                t.Configure(type, new[] { AudioClip.Create("t", 441, 1, 44100, false) });
                return t;
            }
            var chair = Target(InstrumentType.Mbira);
            var lamp = Target(InstrumentType.Erhu);
            var sparkle = Child("Sparkle").gameObject.AddComponent<SparkleTwinkle>();
            sparkle.Configure(chair, 0f);
            yield return null;

            MusicalHitPipeline.Submit(MusicalHitPipeline.BeginAction(), lamp, OrbType.Bop, IntensityTier.Medium, PitchInput.FromPetal(1), InputPath.Place);
            Assert.IsFalse(sparkle.Flaring, "another instrument's note");
            MusicalHitPipeline.Submit(MusicalHitPipeline.BeginAction(), chair, OrbType.Bop, IntensityTier.Medium, PitchInput.FromPetal(1), InputPath.Place);
            Assert.IsTrue(sparkle.Flaring, "its own instrument played");
            yield return new WaitForSeconds(SparkleTwinkle.FlareSeconds * 0.5f);
            Assert.Greater(sparkle.transform.localScale.x, 0.02f * 1.5f, "visibly bigger at the peak");
            yield return new WaitForSeconds(SparkleTwinkle.FlareSeconds);
            Assert.Less(sparkle.transform.localScale.x, 0.02f * 1.05f, "back to a gentle twinkle");
        }

        [UnityTest]
        public IEnumerator ReducedMotionHoldsTheRainforestStill()
        {
            Butterfly.ReducedMotion = true;
            SparkleTwinkle.ReducedMotion = true;
            var flier = MakeButterfly();
            var sparkle = Child("Sparkle").gameObject.AddComponent<SparkleTwinkle>();
            var start = flier.transform.position;
            var size = sparkle.transform.localScale;
            sparkle.Flare();
            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreEqual(start, flier.transform.position);
            Assert.AreEqual(size, sparkle.transform.localScale);
        }
    }
}
