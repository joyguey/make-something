using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>Real components: a hit's note, orb and tier pick exactly one rendered voice, every time.</summary>
    public class MusicGrammarPlayModeTests
    {
        readonly List<GameObject> m_Spawned = new();
        readonly List<MusicalHitEvent> m_Events = new();

        [SetUp]
        public void SetUp()
        {
            MusicalHitPipeline.Reset();
            MusicalHitPipeline.Committed += m_Events.Add;
        }

        [TearDown]
        public void TearDown()
        {
            MusicalHitPipeline.Committed -= m_Events.Add;
            foreach (var go in m_Spawned) if (go != null) Object.Destroy(go);
            m_Spawned.Clear();
            m_Events.Clear();
        }

        InstrumentTarget MakeTarget(InstrumentType type, Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Target_{type}";
            go.transform.position = position;
            go.transform.localScale = new Vector3(2f, 0.2f, 2f);
            go.AddComponent<AudioSource>();
            var target = go.AddComponent<InstrumentTarget>();
            var clips = new AudioClip[NoteMapping.Count];
            for (int i = 0; i < clips.Length; i++)
            {
                var data = new float[22050];
                float freq = 220f * (1f + 0.12f * i);
                for (int s = 0; s < data.Length; s++)
                    data[s] = Mathf.Sin(2f * Mathf.PI * freq * s / 44100f) * Mathf.Exp(-3f * s / 44100f);
                clips[i] = AudioClip.Create($"{type}_{i}", data.Length, 1, 44100, false);
                clips[i].SetData(data, 0);
            }
            target.Configure(type, clips);
            m_Spawned.Add(go);
            return target;
        }

        OrbSelector MakeSelector(params InstrumentTarget[] targets)
        {
            var selector = new GameObject("Selector").AddComponent<OrbSelector>();
            selector.Configure(targets, null);
            m_Spawned.Add(selector.gameObject);
            return selector;
        }

        static string VoiceName(InstrumentTarget t) =>
            t.GetComponents<AudioSource>()[0].clip != null ? t.GetComponents<AudioSource>()[0].clip.name : null;

        [UnityTest]
        public IEnumerator EachPetalPlaysItsOwnNoteWithTheOrbsVoice()
        {
            var chair = MakeTarget(InstrumentType.Mbira, Vector3.zero);
            var selector = MakeSelector(chair);
            yield return null; // Start renders the voice bank
            Assert.IsTrue(chair.HasVoiceBank);

            selector.Select(OrbType.Boom);
            for (int petal = 0; petal < NoteMapping.Count; petal++)
            {
                selector.SetPetal(petal);
                var hit = selector.PlaceOn(chair);
                Assert.AreEqual(petal, hit.Note.Index);
                Assert.AreEqual(chair.VoiceFor(OrbType.Boom, petal), chair.VoiceFor(hit.Orb, hit.Note.Index));
            }
            CollectionAssert.AreEqual(new[] { "D4", "E4", "F#4", "A4", "B4" }, m_Events.ConvertAll(e => e.Note.Label));
        }

        [UnityTest]
        public IEnumerator SameInputSameVoiceDifferentOrbDifferentVoice()
        {
            var lamp = MakeTarget(InstrumentType.Erhu, Vector3.zero);
            var selector = MakeSelector(lamp);
            yield return null;

            selector.SetPetal(3);
            selector.Select(OrbType.Flutter);
            selector.PlaceOn(lamp);
            string first = VoiceName(lamp);
            selector.PlaceOn(lamp);
            selector.PlaceOn(lamp);
            var names = new HashSet<string>();
            foreach (var source in lamp.GetComponents<AudioSource>())
                if (source.clip != null) names.Add(source.clip.name);
            Assert.AreEqual(1, names.Count, "three identical places used one identical voice");
            Assert.AreEqual("Erhu_3_Flutter", first);

            selector.Select(OrbType.Boom);
            Assert.AreNotEqual(lamp.VoiceFor(OrbType.Flutter, 3), lamp.VoiceFor(OrbType.Boom, 3));
            Assert.AreEqual(m_Events[0].Note.Label, m_Events[2].Note.Label);
        }

        [UnityTest]
        public IEnumerator UduPetalsPlayArticulationsFromTheRightSamples()
        {
            var table = MakeTarget(InstrumentType.Udu, Vector3.zero);
            var selector = MakeSelector(table);
            yield return null;

            selector.SetPetal((int)UduArticulation.Tap);
            var hit = selector.PlaceOn(table);
            Assert.AreEqual(UduArticulation.Tap, hit.Note.Articulation);
            Assert.AreEqual($"Udu_{(int)UduArticulation.Tap}_{OrbType.Flutter}", table.VoiceFor(hit.Orb, hit.Note.Index).name);
            Assert.Less(table.VoiceFor(OrbType.Bop, (int)UduArticulation.Tap).length,
                table.VoiceFor(OrbType.Bop, (int)UduArticulation.Open).length, "a tap is shorter than an open tone");
        }

        [UnityTest]
        public IEnumerator TossNoteFollowsContactHeight()
        {
            var target = MakeTarget(InstrumentType.Mbira, Vector3.zero);
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere).AddComponent<Ball>(); // adds its Rigidbody
            m_Spawned.Add(ball.gameObject);
            ball.transform.position = new Vector3(0f, 1f, 0f);
            ball.ApplyOrb(OrbType.Bop);
            yield return null;
            ball.Launch(new Vector3(0f, -4f, 0f));
            float end = Time.time + 1f;
            while (Time.time < end && m_Events.Count == 0) yield return new WaitForFixedUpdate();

            Assert.AreEqual(1, m_Events.Count);
            var hit = m_Events[0];
            Assert.IsFalse(hit.Pitch.IsPetal);
            Assert.AreEqual(NoteMapping.IndexFromHeight(hit.Pitch.Height), hit.Note.Index);
            Assert.AreEqual(4, hit.Note.Index, "landing on the top face is the top band");
        }
    }
}
