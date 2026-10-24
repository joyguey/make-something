using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>Echo orbits with real components and the real audio clock (no headset).</summary>
    public class EchoPlayModeTests
    {
        readonly List<GameObject> m_Spawned = new();
        readonly List<ScheduledEcho> m_Scheduled = new();
        readonly List<MusicalHitEvent> m_Refused = new();
        EchoConductor m_Conductor;

        [SetUp]
        public void SetUp()
        {
            MusicalHitPipeline.Reset();
            m_Scheduled.Clear();
            m_Refused.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in m_Spawned) if (go != null) Object.Destroy(go);
            m_Spawned.Clear();
        }

        InstrumentTarget MakeTarget(InstrumentType type, Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Target_{type}";
            go.transform.position = position;
            go.AddComponent<AudioSource>();
            var target = go.AddComponent<InstrumentTarget>();
            var clips = new AudioClip[NoteMapping.Count];
            for (int i = 0; i < clips.Length; i++)
            {
                var data = new float[8820];
                for (int s = 0; s < data.Length; s++) data[s] = Mathf.Sin(2f * Mathf.PI * 300f * s / 44100f) * (1f - s / (float)data.Length);
                clips[i] = AudioClip.Create($"{type}_{i}", data.Length, 1, 44100, false);
                clips[i].SetData(data, 0);
            }
            target.Configure(type, clips);
            m_Spawned.Add(go);
            return target;
        }

        OrbSelector MakeRig(params InstrumentTarget[] targets)
        {
            var go = new GameObject("Rig");
            m_Spawned.Add(go);
            m_Conductor = go.AddComponent<EchoConductor>();
            m_Conductor.Scheduled += m_Scheduled.Add;
            m_Conductor.Refused += m_Refused.Add;
            var selector = go.AddComponent<OrbSelector>();
            selector.Configure(targets, null);
            return selector;
        }

        IEnumerator WaitUntil(System.Func<bool> condition, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }

        [UnityTest]
        public IEnumerator PlacedHitPlantsAnEchoThatRepeatsExactlyOnTheGrid()
        {
            var lamp = MakeTarget(InstrumentType.Erhu, Vector3.zero);
            var selector = MakeRig(lamp);
            yield return null;
            var hit = selector.PlaceOn(lamp);
            Assert.AreEqual(1, m_Conductor.Sequencer.Echoes.Count);
            var echo = m_Conductor.Sequencer.Echoes[0];
            Assert.AreEqual(hit.Note.Index, echo.NoteIndex);

            yield return WaitUntil(() => m_Scheduled.Count > 0, (float)m_Conductor.Clock.BarDuration + 1f);
            Assert.AreEqual(1, m_Scheduled.Count, "one repeat scheduled within a bar");
            var due = m_Scheduled[0];
            Assert.AreEqual(m_Conductor.Clock.TimeOf(due.Bar, echo.Step), due.DspTime, 1e-9, "on the DSP grid");
            Assert.Greater(due.DspTime, hit.CreatedAtDspTime + m_Conductor.Clock.StepDuration * 0.5, "not a double of the live hit");
            Assert.AreEqual(88.0, m_Conductor.Clock.Bpm);
        }

        [UnityTest]
        public IEnumerator SecondFlutterOnTheLampAddsANoteNotTempo()
        {
            var lamp = MakeTarget(InstrumentType.Erhu, Vector3.zero);
            var selector = MakeRig(lamp);
            yield return null;
            selector.PlaceOn(lamp);
            yield return new WaitForSecondsRealtime(0.7f); // about four steps later
            selector.PlaceOn(lamp);
            var echoes = m_Conductor.Sequencer.Echoes;
            Assert.AreEqual(2, echoes.Count);
            Assert.AreNotEqual(echoes[0].Step, echoes[1].Step, "two positions on the orbit");
            Assert.AreEqual(88.0, m_Conductor.Clock.Bpm, "tempo unchanged");
        }

        [UnityTest]
        public IEnumerator SeventhHitOnOneTargetIsRefusedNotOverwritten()
        {
            var table = MakeTarget(InstrumentType.Udu, Vector3.zero);
            var selector = MakeRig(table);
            yield return null;
            for (int i = 0; i < 7; i++) selector.PlaceOn(table);
            Assert.AreEqual(EchoSequencer.MaxPerTarget, m_Conductor.Sequencer.CountOn(InstrumentType.Udu));
            Assert.AreEqual(1, m_Refused.Count, "the seventh hit played live but did not overwrite an echo");
        }

        [UnityTest]
        public IEnumerator RemovingAnEchoCancelsItsAlreadyScheduledRepeat()
        {
            var chair = MakeTarget(InstrumentType.Mbira, Vector3.zero);
            var selector = MakeRig(chair);
            yield return null;
            selector.PlaceOn(chair);
            yield return WaitUntil(() => chair.ScheduledCount > 0, (float)m_Conductor.Clock.BarDuration + 1f);
            Assert.Greater(chair.ScheduledCount, 0, "a repeat is waiting on the audio clock");

            m_Conductor.Select(m_Conductor.Sequencer.Echoes[0].StableId);
            Assert.IsTrue(m_Conductor.RemoveSelected());
            Assert.AreEqual(0, chair.ScheduledCount, "cancelled before it sounded");
            Assert.AreEqual(0, m_Conductor.Sequencer.Echoes.Count);

            Assert.IsTrue(m_Conductor.Undo());
            Assert.AreEqual(1, m_Conductor.Sequencer.Echoes.Count, "undo brings it back");
        }

        [UnityTest]
        public IEnumerator PauseSilencesAndSchedulesNothingResumeContinues()
        {
            var chair = MakeTarget(InstrumentType.Mbira, Vector3.zero);
            var selector = MakeRig(chair);
            yield return null;
            selector.PlaceOn(chair);
            m_Conductor.TogglePause();
            Assert.IsTrue(m_Conductor.IsPaused);
            Assert.AreEqual(0, chair.ScheduledCount);
            Assert.IsFalse(chair.IsSounding, "silent at once");
            int before = m_Scheduled.Count;
            yield return new WaitForSecondsRealtime((float)m_Conductor.Clock.BarDuration + 0.3f);
            Assert.AreEqual(before, m_Scheduled.Count, "nothing scheduled for a whole bar while paused");

            m_Conductor.TogglePause();
            yield return WaitUntil(() => m_Scheduled.Count > before, (float)m_Conductor.Clock.BarDuration + 1f);
            Assert.Greater(m_Scheduled.Count, before, "the groove resumes");
        }

        [UnityTest]
        public IEnumerator PauseButtonPausesAndRelabelsClearButtonClearsAndUndoRestores()
        {
            var chair = MakeTarget(InstrumentType.Mbira, Vector3.zero);
            var selector = MakeRig(chair);
            var pauseGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var labelGo = new GameObject("PauseText");
            m_Spawned.Add(pauseGo);
            m_Spawned.Add(labelGo);
            var label = labelGo.AddComponent<TMPro.TextMeshPro>();
            label.text = "Pause";
            var pause = pauseGo.AddComponent<MusicControlButton>();
            pause.Configure(MusicControlButton.Kind.Pause, label);
            var clearGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            m_Spawned.Add(clearGo);
            clearGo.AddComponent<MusicControlButton>().Configure(MusicControlButton.Kind.Clear, null);
            yield return null;

            selector.PlaceOn(chair);
            selector.PlaceOn(chair);
            Assert.IsTrue(InstrumentInputRouter.Activate(pauseGo), "tap on Pause handled");
            Assert.IsTrue(m_Conductor.IsPaused);
            Assert.AreEqual("Play", label.text, "label says what the next tap does");
            Assert.IsFalse(chair.IsSounding, "silent at once");
            InstrumentInputRouter.Activate(pauseGo);
            Assert.IsFalse(m_Conductor.IsPaused);
            Assert.AreEqual("Pause", label.text);

            Assert.IsTrue(InstrumentInputRouter.Activate(clearGo), "tap on Clear handled");
            Assert.AreEqual(0, m_Conductor.Sequencer.Echoes.Count);
            Assert.IsTrue(m_Conductor.Undo());
            Assert.AreEqual(2, m_Conductor.Sequencer.Echoes.Count, "undo brings cleared echoes back");
        }

        [UnityTest]
        public IEnumerator EchoMiniSitsOnTheMarkerAtItsStep()
        {
            var chair = MakeTarget(InstrumentType.Mbira, Vector3.zero);
            MakeRig(chair);
            var orbitGo = new GameObject("Orbit");
            m_Spawned.Add(orbitGo);
            orbitGo.transform.position = new Vector3(0f, 1f, 2f);
            var view = orbitGo.AddComponent<OrbitView>();
            Transform[] Pool()
            {
                var pool = new Transform[EchoSequencer.MaxPerTarget];
                for (int i = 0; i < pool.Length; i++)
                {
                    pool[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
                    pool[i].SetParent(orbitGo.transform, false);
                    pool[i].localScale = Vector3.one * 0.05f;
                }
                return pool;
            }
            var bop = Pool();
            view.Configure(chair, orbitGo.transform, Pool(), bop, Pool(), 0.3f);
            yield return null;

            var hit = new MusicalHitEvent(1, 1, InstrumentType.Mbira, OrbType.Bop, IntensityTier.Medium, PitchInput.FromPetal(1), InputPath.Place,
                m_Conductor.Clock.TimeOf(0, 6));
            m_Conductor.Sequencer.TryAdd(hit, out var echo);
            view.Sync(m_Conductor.Sequencer.Echoes, 6 / 16.0, 0);
            Assert.Less((bop[0].position - view.MarkerPosition).magnitude, 1e-4f, "the Bop mini is at the marker when its step plays");
            view.Sync(m_Conductor.Sequencer.Echoes, 10 / 16.0, echo.StableId);
            Assert.Greater((bop[0].position - view.MarkerPosition).magnitude, 0.1f, "and has moved on later in the bar");
            Assert.Greater(bop[0].localScale.x, 0.05f, "selected echo is shown larger");
        }
    }
}
