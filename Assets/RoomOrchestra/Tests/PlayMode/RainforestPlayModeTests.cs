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
        public IEnumerator AFlowerGrowsAsABudThenBloomsAndFallsIntoStep()
        {
            var conductorGo = new GameObject("Conductor");
            conductorGo.transform.SetParent(m_Root.transform, false);
            conductorGo.AddComponent<EchoConductor>();
            var root = new GameObject("Flower").transform;
            root.SetParent(m_Root.transform, false);
            root.localScale = Vector3.one * SproutSync.HiddenScale;
            var look = Child("Visual", root);
            var bloom = Child("Bloom", look);
            bloom.localScale = Vector3.one * FloorFlower.ClosedScale;
            var bud = Child("Bud", look);
            var flower = root.gameObject.AddComponent<FloorFlower>();
            flower.Configure(look, 1f);
            flower.ConfigureBloom(bloom, bud);
            while (Time.timeSinceLevelLoad < JellyBody.StartDelaySeconds) yield return null;

            flower.Sprout(0f, 0f, 1f); // no delay, quickest pace, half a beat late
            yield return new WaitForSeconds(SproutSync.MinGrowSeconds * 0.6f);
            Assert.Greater(root.localScale.x, 0.3f, "growing out of the ground");
            Assert.AreEqual(FloorFlower.ClosedScale, bloom.localScale.x, 1e-3f, "still a closed bud while it grows");
            Assert.AreEqual(1f, bud.localScale.x, 1e-3f, "the bud is showing");
            Assert.Greater(System.Math.Abs(flower.OffsetBeats), 0.4f, "starts off the beat");

            yield return new WaitForSeconds(SproutSync.MinGrowSeconds * 0.4f + FloorFlower.BloomSeconds + 0.1f);
            Assert.AreEqual(1f, bloom.localScale.x, 1e-3f, "in full bloom");
            Assert.AreEqual(FloorFlower.ClosedScale, bud.localScale.x, 1e-3f, "the bud has tucked away");
            yield return new WaitForSeconds(SproutSync.SettleSeconds);
            Assert.Less(System.Math.Abs(flower.OffsetBeats), 0.03f, "in step with the rest after a few seconds");
        }

        [UnityTest]
        public IEnumerator ToadstoolsSproutAndDanceOnTheBeat()
        {
            var conductorGo = new GameObject("Conductor");
            conductorGo.transform.SetParent(m_Root.transform, false);
            conductorGo.AddComponent<EchoConductor>();
            var shroom = new GameObject("Toadstool").transform;
            shroom.SetParent(m_Root.transform, false);
            shroom.localScale = Vector3.one * SproutSync.HiddenScale;
            var look = Child("Visual", shroom);
            look.localScale = Vector3.one;
            var toadstool = shroom.gameObject.AddComponent<Toadstool>();
            toadstool.Configure(look, 1.2f);
            for (int i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(SproutSync.HiddenScale, shroom.localScale.x, 1e-6f, "out of sight until the music grows it");
            Assert.AreEqual(Quaternion.identity, look.localRotation, "a hidden sprout does not dance");

            toadstool.Sprout(0f, 0f, 0.5f); // no delay, the quickest pace, on the beat
            yield return new WaitForSeconds(SproutSync.MinGrowSeconds + 0.1f);
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
            director.Configure(uduVine, null, lampVine, shrooms, new Butterfly[0]);

            for (int i = 0; i < JungleGrowth.NotesPerVine / 2; i++) director.NoteAdded(InstrumentType.Udu);
            Assert.AreEqual(4, uduVine.Shown, "half the notes: half the table vine");
            Assert.AreEqual(0, lampVine.Shown, "the lamp waits for erhu");
            Assert.AreEqual(System.Linq.Enumerable.Count(JungleGrowth.MushroomAt, n => n <= JungleGrowth.NotesPerVine / 2), System.Linq.Enumerable.Count(shrooms, s => s.Sprouted));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheLoopRepeatingDoesNotGrowTheJungleOnlyNewNotesDo()
        {
            var conductorGo = new GameObject("Conductor");
            conductorGo.transform.SetParent(m_Root.transform, false);
            var conductor = conductorGo.AddComponent<EchoConductor>();
            var tableGo = new GameObject("Table");
            tableGo.transform.SetParent(m_Root.transform, false);
            tableGo.AddComponent<BoxCollider>();
            tableGo.AddComponent<AudioSource>();
            var table = tableGo.AddComponent<InstrumentTarget>();
            table.Configure(InstrumentType.Udu, new[] { AudioClip.Create("t", 441, 1, 44100, false) });
            var vine = MakeVine(8, out _, out _);
            var director = m_Root.AddComponent<JungleDirector>();
            director.Configure(vine, null, null, new Toadstool[0], new Butterfly[0]);
            yield return null;

            for (int n = 0; n < JungleGrowth.NotesBeforeGrowth; n++)
                MusicalHitPipeline.Submit(MusicalHitPipeline.BeginAction(), table, OrbType.Boom, IntensityTier.Medium, PitchInput.FromPetal(n), InputPath.Place);
            Assert.AreEqual(JungleGrowth.NotesBeforeGrowth, conductor.Sequencer.Echoes.Count, "the notes joined the song");
            Assert.AreEqual(JungleGrowth.NotesBeforeGrowth, director.Growth.TotalNotes, "new notes grow the jungle");
            int shown = vine.Shown;
            Assert.Greater(shown, 0);

            yield return new WaitForSeconds(3.2f); // more than a bar: the echoes come round again
            Assert.AreEqual(JungleGrowth.NotesBeforeGrowth, director.Growth.TotalNotes, "repeats of the loop add nothing");
            Assert.AreEqual(shown, vine.Shown, "the vine waits for the next new note");

            MusicalHitPipeline.Submit(MusicalHitPipeline.BeginAction(), table, OrbType.Boom, IntensityTier.Medium, PitchInput.FromPetal(4), InputPath.Place);
            Assert.AreEqual(JungleGrowth.NotesBeforeGrowth + 1, director.Growth.TotalNotes);
            Assert.Greater(vine.Shown, shown, "and grows again when the song does");
        }

        [UnityTest]
        public IEnumerator ClearSweepsTheJungleAwayInSmoke()
        {
            var conductorGo = new GameObject("Conductor");
            conductorGo.transform.SetParent(m_Root.transform, false);
            var conductor = conductorGo.AddComponent<EchoConductor>();
            var clouds = new PoofCloud[6];
            for (int c = 0; c < clouds.Length; c++)
            {
                var cloudGo = new GameObject($"Cloud{c}");
                cloudGo.transform.SetParent(m_Root.transform, false);
                var puffs = new Transform[7];
                for (int i = 0; i < puffs.Length; i++) puffs[i] = Child($"Puff{i}", cloudGo.transform);
                clouds[c] = cloudGo.AddComponent<PoofCloud>();
                clouds[c].Configure(puffs, null);
            }
            var vine = MakeVine(8, out var pieces, out var bloom);
            var shroomGo = new GameObject("Toadstool").transform;
            shroomGo.SetParent(m_Root.transform, false);
            shroomGo.localScale = Vector3.one * SproutSync.HiddenScale;
            var shroom = shroomGo.gameObject.AddComponent<Toadstool>();
            shroom.Configure(Child("Visual", shroomGo), 1f);
            var flier = MakeButterfly();
            var director = m_Root.AddComponent<JungleDirector>();
            director.Configure(vine, null, null, new[] { shroom }, new[] { flier });

            for (int i = 0; i < JungleGrowth.NotesPerVine; i++) director.NoteAdded(InstrumentType.Udu);
            // A layered song (two instruments, four echoes) calls the butterfly out.
            for (int i = 0; i < 4; i++)
            {
                var hit = new MusicalHitEvent(i + 1, i + 1, i < 2 ? InstrumentType.Udu : InstrumentType.Erhu, OrbType.Bop,
                    IntensityTier.Medium, PitchInput.FromPetal(i), InputPath.Place, conductor.Clock.TimeOf(0, i * 3));
                conductor.Sequencer.TryAdd(hit, out _);
            }
            yield return new WaitForSeconds(3.5f);
            Assert.AreEqual(8, vine.Shown);
            Assert.IsTrue(shroom.Sprouted);
            Assert.IsFalse(flier.AtHome);

            conductor.Clear();
            int playing = System.Linq.Enumerable.Count(clouds, c => c.Playing);
            Assert.GreaterOrEqual(playing, 3, "a cloud over the vine, the toadstool and the butterfly");
            Assert.Greater(System.Linq.Enumerable.Max(clouds, c => c.transform.localScale.x), 2f, "big smoke, not a little landing poof");
            Assert.AreEqual(8, vine.Shown, "still there for a moment, under the smoke");

            yield return new WaitForSeconds(JungleDirector.VanishDelay + 0.1f);
            Assert.AreEqual(0, vine.Shown, "vine folded away");
            foreach (var p in pieces) Assert.AreEqual(VineGrowth.HiddenScale, p.localScale.x, 1e-6f, p.name);
            Assert.AreEqual(VineGrowth.HiddenScale, bloom.localScale.x, 1e-6f, "flowers gone too");
            Assert.IsFalse(shroom.Sprouted);
            Assert.AreEqual(SproutSync.HiddenScale, shroomGo.localScale.x, 1e-6f, "back out of sight");
            yield return null;
            Assert.IsTrue(flier.AtHome);
            Assert.Less((flier.transform.position - Home).magnitude, 1e-4f, "butterfly vanished home into the lamp");
            Assert.AreEqual(0, director.Growth.TotalNotes, "the next song grows a new jungle");

            yield return new WaitForSeconds(PoofCloud.Seconds);
            foreach (var c in clouds) Assert.AreEqual(Vector3.one, c.transform.localScale, "clouds shrink back to fit inside the stand");
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
