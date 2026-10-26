using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>The rainforest growing with the music, with real transforms (no headset).</summary>
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
            VineGrowth.ReducedMotion = false;
            Toadstool.ReducedMotion = false;
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

        static readonly Vector3 Home = new Vector3(1.1f, 1.3f, 2.3f);

        Butterfly MakeButterfly()
        {
            var body = new GameObject("Butterfly").transform;
            body.SetParent(m_Root.transform, false);
            body.localPosition = Home;
            var flier = body.gameObject.AddComponent<Butterfly>();
            var trail = new Transform[8];
            for (int i = 0; i < trail.Length; i++)
            {
                trail[i] = Child($"Trail{i}");
                trail[i].localPosition = Home;
            }
            flier.Configure(Child("WingL", body), Child("WingR", body), trail, new Vector3(0f, 1.5f, 2.3f), new Vector3(1.4f, 0.25f, 0.45f), 10f, 0f, Home);
            return flier;
        }

        [UnityTest]
        public IEnumerator ButterfliesStayHomeUntilCalledThenFlyFlapAndTrail()
        {
            var flier = MakeButterfly();
            for (int i = 0; i < 20; i++) yield return null;
            Assert.AreEqual(Home, flier.transform.position, "waits inside the lamp until the song calls");

            flier.SetOut(true);
            float minWing = 999f, maxWing = -999f;
            for (float end = Time.time + 4f; Time.time < end;)
            {
                yield return null;
                float lift = Mathf.DeltaAngle(0f, flier.WingL.localEulerAngles.z);
                minWing = Mathf.Min(minWing, lift);
                maxWing = Mathf.Max(maxWing, lift);
            }
            Assert.Greater((flier.transform.position - Home).magnitude, 0.3f, "out of the lamp and away on its loop");
            Assert.Greater(maxWing - minWing, 20f, "flaps");
            Assert.Less(maxWing, 0f, "the left wing always stays up (a V)");
            var now = flier.transform.position;
            Assert.Greater(Vector3.Distance(flier.Trail[7].position, now), Vector3.Distance(flier.Trail[0].position, now), "the trail stretches out behind it");
            Assert.Less(flier.Trail[7].localScale.x, flier.Trail[0].localScale.x, "older sparkles are smaller");

            flier.SetOut(false);
            yield return new WaitForSeconds(3.3f);
            Assert.IsTrue(flier.AtHome);
            Assert.Less((flier.transform.position - Home).magnitude, 1e-4f, "flew home into the lamp");
            foreach (var dot in flier.Trail) Assert.Less((dot.position - Home).magnitude, 1e-4f, "trail tucked in too");
        }

        VineGrowth MakeVine(int pieces, out Transform[] segments, out Transform bloom)
        {
            var vine = new GameObject("Vine").transform;
            vine.SetParent(m_Root.transform, false);
            segments = new Transform[pieces];
            for (int i = 0; i < pieces; i++)
            {
                segments[i] = Child($"Seg{i}", vine);
                segments[i].localScale = Vector3.one * VineGrowth.HiddenScale;
            }
            bloom = Child("Bloom", vine);
            bloom.localScale = Vector3.one * VineGrowth.HiddenScale;
            var growth = vine.gameObject.AddComponent<VineGrowth>();
            growth.Configure(segments, new[] { bloom });
            return growth;
        }

        [UnityTest]
        public IEnumerator VinesCreepPieceByPieceAndBloomAtTheTop()
        {
            var vine = MakeVine(8, out var segments, out var bloom);
            Assert.IsTrue(vine.SetProgress(1f / 8f), "the first notes start the vine");
            Assert.IsFalse(vine.SetProgress(1f / 8f), "same progress: nothing new");
            yield return new WaitForSeconds(VineGrowth.GrowSeconds + 0.1f);
            Assert.AreEqual(1f, segments[0].localScale.x, 1e-3f, "first piece unfurled");
            Assert.AreEqual(VineGrowth.HiddenScale, segments[1].localScale.x, 1e-6f, "the rest still folded away");
            Assert.AreEqual(VineGrowth.HiddenScale, bloom.localScale.x, 1e-6f, "no flowers yet");

            vine.SetProgress(1f);
            yield return null;
            Assert.Greater(segments[1].localScale.x, VineGrowth.HiddenScale, "next piece is already creeping");
            Assert.AreEqual(VineGrowth.HiddenScale, segments[7].localScale.x, 1e-6f, "the tip waits its turn");
            yield return new WaitForSeconds(VineGrowth.StaggerSeconds * 7 + VineGrowth.GrowSeconds * 1.8f);
            foreach (var s in segments) Assert.AreEqual(1f, s.localScale.x, 1e-3f, s.name);
            Assert.IsTrue(vine.Bloomed);
            Assert.AreEqual(1f, bloom.localScale.x, 1e-3f, "flowers bloom once the vine is full grown");
        }

        [UnityTest]
        public IEnumerator ToadstoolsSproutAndDanceOnTheBeat()
        {
            var conductorGo = new GameObject("Conductor");
            conductorGo.transform.SetParent(m_Root.transform, false);
            conductorGo.AddComponent<EchoConductor>();
            var shroom = new GameObject("Toadstool").transform;
            shroom.SetParent(m_Root.transform, false);
            shroom.localScale = Vector3.one * Toadstool.BudScale;
            var look = Child("Visual", shroom);
            look.localScale = Vector3.one;
            var toadstool = shroom.gameObject.AddComponent<Toadstool>();
            toadstool.Configure(look, 1.2f);
            for (int i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(Toadstool.BudScale, shroom.localScale.x, 1e-6f, "a bud until the music grows it");
            Assert.AreEqual(Quaternion.identity, look.localRotation, "buds do not dance");

            toadstool.Sprout();
            yield return new WaitForSeconds(Toadstool.SproutSeconds + 0.1f);
            Assert.AreEqual(1.2f, shroom.localScale.x, 1e-3f, "full grown");
            float minSway = 999f, maxSway = -999f, minY = 9f, maxY = 0f;
            for (float end = Time.time + 2.8f; Time.time < end;) // about one bar at 88 BPM
            {
                yield return null;
                float sway = Mathf.DeltaAngle(0f, look.localEulerAngles.z);
                minSway = Mathf.Min(minSway, sway);
                maxSway = Mathf.Max(maxSway, sway);
                minY = Mathf.Min(minY, look.localScale.y);
                maxY = Mathf.Max(maxY, look.localScale.y);
            }
            Assert.Greater(maxSway - minSway, 10f, "sways left and right");
            Assert.Greater(maxY - minY, 0.08f, "squashes on the beats");
        }

        [UnityTest]
        public IEnumerator EachNoteGrowsItsOwnVineAndWakesTheFloor()
        {
            var uduVine = MakeVine(8, out var uduPieces, out _);
            var lampVine = MakeVine(8, out _, out _);
            var shrooms = new Toadstool[JungleGrowth.MushroomAt.Length];
            for (int i = 0; i < shrooms.Length; i++)
            {
                var s = new GameObject($"Toadstool{i}").transform;
                s.SetParent(m_Root.transform, false);
                shrooms[i] = s.gameObject.AddComponent<Toadstool>();
                shrooms[i].Configure(Child("Visual", s), 1f);
            }
            var director = m_Root.AddComponent<JungleDirector>();
            director.Configure(uduVine, null, lampVine, null, shrooms, new Butterfly[0]);

            for (int i = 0; i < JungleGrowth.NotesPerVine / 4; i++) director.Heard(InstrumentType.Udu);
            Assert.AreEqual(2, uduVine.Shown, "a quarter of the notes: a quarter of the table vine");
            Assert.AreEqual(0, lampVine.Shown, "the lamp waits for erhu");
            Assert.AreEqual(System.Linq.Enumerable.Count(JungleGrowth.MushroomAt, n => n <= JungleGrowth.NotesPerVine / 4), System.Linq.Enumerable.Count(shrooms, s => s.Sprouted));
            yield return null;
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
            flier.SetOut(true);
            var sparkle = Child("Sparkle").gameObject.AddComponent<SparkleTwinkle>();
            var size = sparkle.transform.localScale;
            sparkle.Flare();
            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreEqual(Home, flier.transform.position);
            Assert.AreEqual(size, sparkle.transform.localScale);
        }
    }
}
