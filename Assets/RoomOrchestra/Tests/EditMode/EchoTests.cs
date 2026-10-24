using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace RoomOrchestra.Tests
{
    public class MusicClockTests
    {
        [Test]
        public void EightyEightBpmFourFourSixteenSteps()
        {
            var clock = new MusicClock(10.0);
            Assert.AreEqual(88.0, clock.Bpm);
            Assert.AreEqual(60.0 / 88.0 / 4.0, clock.StepDuration, 1e-12);
            Assert.AreEqual(clock.StepDuration * 16, clock.BarDuration, 1e-12);
            Assert.AreEqual(clock.BarDuration / 4, clock.BeatDuration, 1e-12);
        }

        [TestCase(3.4, 0, 3)]
        [TestCase(3.6, 0, 4)]
        [TestCase(15.4, 0, 15)]
        [TestCase(15.6, 1, 0)]
        [TestCase(16 * 5 + 2.2, 5, 2)]
        public void NearestStepQuantizesAndWrapsIntoNextBar(double steps, long expectedBar, int expectedStep)
        {
            var clock = new MusicClock(10.0);
            clock.Nearest(10.0 + steps * clock.StepDuration, out long bar, out int step);
            Assert.AreEqual(expectedBar, bar);
            Assert.AreEqual(expectedStep, step);
        }

        [Test]
        public void NextBarAfterIsStrictlyLater()
        {
            var clock = new MusicClock(0.0);
            double onStep = clock.TimeOf(3, 7);
            Assert.AreEqual(4, clock.NextBarAfter(7, onStep), "exactly on the step -> next bar");
            Assert.AreEqual(3, clock.NextBarAfter(7, onStep - 1e-6));
        }

        [Test]
        public void PauseFreezesPhaseAndResumeContinuesFromIt()
        {
            var clock = new MusicClock(0.0);
            double t = clock.TimeOf(2, 5) + 0.01;
            double phase = clock.BarPhase(t);
            clock.Pause(t);
            Assert.AreEqual(phase, clock.BarPhase(t + 7.0), 1e-12, "frozen while paused");
            clock.Resume(t + 7.0);
            Assert.AreEqual(phase, clock.BarPhase(t + 7.0), 1e-12, "continues where it stopped");
            Assert.AreEqual(7.0, clock.StartDsp, 1e-12);
        }
    }

    public class EchoSequencerTests
    {
        const double Start = 100.0;
        long m_Id;

        MusicClock m_Clock;
        EchoSequencer m_Seq;

        [SetUp]
        public void SetUp()
        {
            m_Id = 0;
            m_Clock = new MusicClock(Start);
            m_Seq = new EchoSequencer(m_Clock);
        }

        MusicalHitEvent Hit(InstrumentType target, double dsp, OrbType orb = OrbType.Flutter, int petal = 2,
            IntensityTier tier = IntensityTier.Medium)
        {
            m_Id++;
            return new MusicalHitEvent(m_Id, m_Id, target, orb, tier, PitchInput.FromPetal(petal), InputPath.Place, dsp);
        }

        EchoEvent Add(InstrumentType target, long bar, int step, OrbType orb = OrbType.Flutter, int petal = 2)
        {
            Assert.AreEqual(EchoAddResult.Added, m_Seq.TryAdd(Hit(target, m_Clock.TimeOf(bar, step), orb, petal), out var echo));
            return echo;
        }

        // Calls Collect every frameSeconds from 'from' to 'to'; returns everything scheduled.
        List<ScheduledEcho> Run(double from, double to, double frameSeconds = 1.0 / 72.0)
        {
            var all = new List<ScheduledEcho>();
            for (double t = from; t <= to; t += frameSeconds) all.AddRange(m_Seq.Collect(t));
            return all;
        }

        [Test]
        public void EchoKeepsTheHitsOrbNoteAndTier()
        {
            m_Seq.TryAdd(Hit(InstrumentType.Erhu, m_Clock.TimeOf(0, 4), OrbType.Boom, petal: 3, tier: IntensityTier.Strong), out var echo);
            Assert.AreEqual(OrbType.Boom, echo.Orb);
            Assert.AreEqual(3, echo.NoteIndex);
            Assert.AreEqual(IntensityTier.Strong, echo.Tier);
            Assert.AreEqual(4, echo.Step);
        }

        [Test]
        public void TwoFlutterEchoesOnTheLampFillTheBarTempoUnchanged()
        {
            double bpm = m_Clock.Bpm;
            Add(InstrumentType.Erhu, 0, 0);
            Add(InstrumentType.Erhu, 0, 8);
            var played = Run(m_Clock.TimeOf(0, 9), m_Clock.TimeOf(3, 9));
            Assert.AreEqual(bpm, m_Clock.Bpm, "more echoes never change tempo");
            Assert.AreEqual(6, played.Count, "two notes per bar for three bars");
            var times = played.Select(p => p.DspTime).OrderBy(t => t).ToArray();
            for (int i = 1; i < times.Length; i++)
                Assert.AreEqual(m_Clock.StepDuration * 8, times[i] - times[i - 1], 1e-9, "evenly spaced, half a bar apart");
        }

        [Test]
        public void TheLiveHitIsNotDoubledByItsFirstEcho()
        {
            double hitTime = m_Clock.TimeOf(2, 5);
            m_Seq.TryAdd(Hit(InstrumentType.Mbira, hitTime), out var echo);
            var played = Run(hitTime, m_Clock.TimeOf(3, 5) - 0.31);
            Assert.AreEqual(0, played.Count, "nothing more in the bar where the live note just sounded");
            var next = Run(m_Clock.TimeOf(3, 5) - 0.3, m_Clock.TimeOf(3, 5));
            Assert.AreEqual(1, next.Count);
            Assert.AreEqual(m_Clock.TimeOf(3, 5), next[0].DspTime, 1e-9, "first repeat exactly one bar later");
        }

        [Test]
        public void SixPerTargetAndTheSeventhIsRefusedNotOverwritten()
        {
            for (int i = 0; i < EchoSequencer.MaxPerTarget; i++) Add(InstrumentType.Udu, 0, i * 2);
            var before = m_Seq.Echoes.Select(e => e.StableId).ToArray();
            Assert.AreEqual(EchoAddResult.Full, m_Seq.TryAdd(Hit(InstrumentType.Udu, m_Clock.TimeOf(1, 3)), out var none));
            Assert.IsNull(none);
            CollectionAssert.AreEqual(before, m_Seq.Echoes.Select(e => e.StableId).ToArray());
            Assert.AreEqual(EchoAddResult.Added, m_Seq.TryAdd(Hit(InstrumentType.Mbira, m_Clock.TimeOf(1, 3)), out _), "other targets still accept");
        }

        [Test]
        public void OccupiedStepMovesToNearestFreeStepOnThatTargetOnly()
        {
            Assert.AreEqual(5, Add(InstrumentType.Erhu, 0, 5).Step);
            Assert.AreEqual(6, Add(InstrumentType.Erhu, 1, 5).Step, "+1 first");
            Assert.AreEqual(4, Add(InstrumentType.Erhu, 2, 5).Step, "then -1");
            Assert.AreEqual(5, Add(InstrumentType.Mbira, 2, 5).Step, "another target may share the step");
        }

        [Test]
        public void ThreeTargetsLayerOnTheSameBeat()
        {
            Add(InstrumentType.Udu, 0, 0, OrbType.Boom);
            Add(InstrumentType.Mbira, 0, 0, OrbType.Bop);
            Add(InstrumentType.Erhu, 0, 0, OrbType.Flutter);
            var played = Run(m_Clock.TimeOf(0, 1), m_Clock.TimeOf(1, 1));
            Assert.AreEqual(3, played.Count);
            Assert.AreEqual(1, played.Select(p => p.DspTime).Distinct().Count(), "all three on the same downbeat");
            CollectionAssert.AreEquivalent(new[] { InstrumentType.Udu, InstrumentType.Mbira, InstrumentType.Erhu }, played.Select(p => p.Echo.Target));
        }

        [Test]
        public void NoDriftOverAThousandBarsWithIrregularFrames()
        {
            Add(InstrumentType.Udu, 0, 0);
            Add(InstrumentType.Mbira, 0, 3);
            Add(InstrumentType.Erhu, 0, 11);
            var rng = new Random(1234);
            var played = new List<ScheduledEcho>();
            double t = m_Clock.TimeOf(0, 12);
            double end = m_Clock.TimeOf(1001, 0);
            while (t < end)
            {
                played.AddRange(m_Seq.Collect(t));
                t += 0.005 + rng.NextDouble() * 0.04; // 5-45 ms frames
            }
            // The lookahead may already hand over bar 1001 for the downbeat echo; judge bars 1-1000.
            foreach (var group in played.Where(p => p.Bar <= 1000).GroupBy(p => p.Echo.StableId))
            {
                var bars = group.Select(p => p.Bar).ToArray();
                Assert.AreEqual(1000, bars.Length, "one occurrence per bar, none missing, none doubled");
                for (int i = 0; i < bars.Length; i++)
                {
                    Assert.AreEqual(i + 1, bars[i]);
                    var p = group.ElementAt(i);
                    Assert.AreEqual(m_Clock.TimeOf(p.Bar, p.Echo.Step), p.DspTime, 1e-9, "exactly on the grid");
                }
            }
        }

        [Test]
        public void PausedClockSchedulesNothingAndResumeKeepsTheGroove()
        {
            var echo = Add(InstrumentType.Erhu, 0, 4);
            double pauseAt = m_Clock.TimeOf(1, 0);
            Run(m_Clock.TimeOf(0, 5), pauseAt);
            m_Clock.Pause(pauseAt);
            Assert.AreEqual(0, Run(pauseAt, pauseAt + 20).Count);
            m_Clock.Resume(pauseAt + 20);
            var played = Run(pauseAt + 20, pauseAt + 20 + m_Clock.BarDuration);
            Assert.AreEqual(1, played.Count);
            Assert.AreEqual(m_Clock.TimeOf(1, 4), played[0].DspTime, 1e-9, "same step after the pause");
            Assert.AreEqual(pauseAt + 20 + 4 * m_Clock.StepDuration, played[0].DspTime, 1e-9);
        }

        [Test]
        public void LongHitchSkipsMissedRepeatsInsteadOfBursting()
        {
            Add(InstrumentType.Udu, 0, 0);
            Add(InstrumentType.Udu, 0, 8);
            m_Seq.Collect(m_Clock.TimeOf(0, 9));
            var after = m_Seq.Collect(m_Clock.TimeOf(5, 3)); // ~13 s later
            Assert.LessOrEqual(after.Count, 2, "at most one per echo, never the missed backlog");
            Assert.IsTrue(after.All(p => p.DspTime >= m_Clock.TimeOf(5, 3) - m_Seq.LateTolerance));
        }

        [Test]
        public void RemoveAndUndo()
        {
            var a = Add(InstrumentType.Mbira, 0, 2);
            var b = Add(InstrumentType.Mbira, 0, 6);
            Assert.IsTrue(m_Seq.Remove(a.StableId));
            Assert.AreEqual(1, m_Seq.CountOn(InstrumentType.Mbira));
            Assert.IsTrue(m_Seq.Undo(m_Clock.TimeOf(3, 0)));
            CollectionAssert.AreEqual(new[] { a.StableId, b.StableId }, m_Seq.Echoes.Select(e => e.StableId).ToArray());
            Assert.IsFalse(m_Seq.Remove(999));
        }

        [Test]
        public void NudgeMovesOneStepSkipsOccupiedWrapsAndUndoes()
        {
            var a = Add(InstrumentType.Erhu, 0, 15);
            Add(InstrumentType.Erhu, 0, 0);
            Assert.IsTrue(m_Seq.Nudge(a.StableId, +1, m_Clock.TimeOf(1, 0)));
            Assert.AreEqual(1, a.Step, "15 -> (0 taken) -> 1, wrapping");
            Assert.IsTrue(m_Seq.Undo(m_Clock.TimeOf(1, 1)));
            Assert.AreEqual(15, a.Step);
            Assert.IsTrue(m_Seq.Nudge(a.StableId, -1, m_Clock.TimeOf(1, 2)));
            Assert.AreEqual(14, a.Step);
        }

        [Test]
        public void NudgedEchoPlaysAtItsNewStepNextTime()
        {
            var a = Add(InstrumentType.Mbira, 0, 4);
            double now = m_Clock.TimeOf(1, 0);
            m_Seq.Nudge(a.StableId, +1, now);
            var played = Run(now, m_Clock.TimeOf(2, 0));
            Assert.AreEqual(1, played.Count);
            Assert.AreEqual(m_Clock.TimeOf(1, 5), played[0].DspTime, 1e-9);
        }

        [Test]
        public void ClearAndUndoRestoresEverythingResetForgetsHistory()
        {
            Add(InstrumentType.Udu, 0, 0);
            Add(InstrumentType.Erhu, 0, 4);
            m_Seq.Clear();
            Assert.AreEqual(0, m_Seq.Echoes.Count);
            Assert.AreEqual(0, Run(m_Clock.TimeOf(1, 0), m_Clock.TimeOf(3, 0)).Count, "silence after clear");
            m_Seq.Undo(m_Clock.TimeOf(3, 0));
            Assert.AreEqual(2, m_Seq.Echoes.Count);
            m_Seq.Reset();
            Assert.AreEqual(0, m_Seq.Echoes.Count);
            Assert.IsFalse(m_Seq.CanUndo);
        }

        [Test]
        public void StableIdsAreDeterministic()
        {
            var a = Add(InstrumentType.Udu, 0, 1);
            var b = Add(InstrumentType.Udu, 0, 3);
            Assert.AreEqual(1, a.StableId);
            Assert.AreEqual(2, b.StableId);
        }
    }
}
