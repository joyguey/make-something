using System.Linq;
using NUnit.Framework;

namespace RoomOrchestra.Tests
{
    /// <summary>The guided song's rules: one bar of "Mary Had a Little Lamb", progress read from the song itself.</summary>
    public class TutorialPlanTests
    {
        MusicClock m_Clock;
        EchoSequencer m_Seq;
        long m_Id;

        [SetUp]
        public void SetUp()
        {
            m_Clock = new MusicClock(100.0);
            m_Seq = new EchoSequencer(m_Clock);
            m_Id = 0;
        }

        // A note played at a sloppy time (step 7), then snapped to where the plan wants it.
        EchoEvent Play(InstrumentType target, int petal, int snapTo = -1)
        {
            m_Id++;
            var hit = new MusicalHitEvent(m_Id, m_Id, target, OrbType.Flutter, IntensityTier.Medium, PitchInput.FromPetal(petal), InputPath.Place, m_Clock.TimeOf(0, 7));
            Assert.AreEqual(EchoAddResult.Added, m_Seq.TryAdd(hit, out var echo));
            if (snapTo >= 0) Assert.IsTrue(m_Seq.MoveTo(echo.StableId, snapTo, 100.0, undoable: false));
            return echo;
        }

        [Test]
        public void TheSongIsOneBarOfFiveNoteTonesWithEachStepFree()
        {
            Assert.AreEqual(11, TutorialPlan.Song.Length);
            foreach (var group in TutorialPlan.Song.GroupBy(s => s.Instrument))
                Assert.AreEqual(group.Count(), group.Select(s => s.BarStep).Distinct().Count(), $"{group.Key} steps don't collide");
            Assert.IsTrue(TutorialPlan.Song.All(s => s.BarStep >= 0 && s.BarStep < MusicClock.StepsPerBar));
            Assert.IsTrue(TutorialPlan.Song.All(s => s.GlowNote >= 0 && s.GlowNote < NoteMapping.Count));
            Assert.AreEqual(InstrumentType.Mbira, TutorialPlan.Song[0].Instrument, "the chair first");
            Assert.IsTrue(TutorialPlan.Song.Where(s => s.Introduces).All(s => s.Instrument != InstrumentType.Mbira));
        }

        [Test]
        public void InstrumentsJoinWhenTheSongReachesThem()
        {
            Assert.IsTrue(TutorialPlan.IntroducedBy(InstrumentType.Mbira, 0));
            Assert.IsFalse(TutorialPlan.IntroducedBy(InstrumentType.Erhu, 3));
            Assert.IsTrue(TutorialPlan.IntroducedBy(InstrumentType.Erhu, 4));
            Assert.IsFalse(TutorialPlan.IntroducedBy(InstrumentType.Udu, 5));
            Assert.IsTrue(TutorialPlan.IntroducedBy(InstrumentType.Udu, 6));
        }

        [Test]
        public void ProgressFollowsTheSongThroughUndoAndClear()
        {
            var plan = new TutorialPlan();
            plan.FinishWarmup();
            Assert.IsTrue(plan.Wants(InstrumentType.Mbira, 2));
            Assert.IsFalse(plan.Wants(InstrumentType.Mbira, 1), "E isn't the first note");
            Assert.IsFalse(plan.Wants(InstrumentType.Erhu, 2), "nor the lamp's F#");

            Play(InstrumentType.Mbira, 2, snapTo: 0);
            Assert.IsTrue(plan.Sync(m_Seq.Echoes));
            Assert.AreEqual(1, plan.Index);
            Play(InstrumentType.Mbira, 1, snapTo: 2);
            plan.Sync(m_Seq.Echoes);
            Assert.AreEqual(2, plan.Index);

            Assert.IsTrue(m_Seq.Undo(101.0), "one Undo takes the snapped note back whole");
            Assert.AreEqual(1, m_Seq.Echoes.Count);
            plan.Sync(m_Seq.Echoes);
            Assert.AreEqual(1, plan.Index);

            m_Seq.Clear();
            plan.Sync(m_Seq.Echoes);
            Assert.AreEqual(0, plan.Index, "Clear starts the song over");
            m_Seq.Undo(102.0);
            plan.Sync(m_Seq.Echoes);
            Assert.AreEqual(1, plan.Index, "and Clear's undo brings it back");
        }

        [Test]
        public void ANoteOffItsPlaceDoesNotCount()
        {
            var plan = new TutorialPlan();
            plan.FinishWarmup();
            Play(InstrumentType.Mbira, 2); // left at step 7
            Assert.IsFalse(plan.Sync(m_Seq.Echoes));
            Assert.AreEqual(0, plan.Index);
        }

        [Test]
        public void AnyUduSoundKeepsTheBeatAndTheEndStaysFinished()
        {
            var plan = new TutorialPlan();
            plan.FinishWarmup();
            int i = 0;
            foreach (var step in TutorialPlan.Song)
                Play(step.Instrument, step.Note == TutorialPlan.AnyNote ? i++ % NoteMapping.Count : step.Note, step.BarStep);
            Assert.IsTrue(plan.Sync(m_Seq.Echoes));
            Assert.IsTrue(plan.Done);
            Assert.IsFalse(plan.Skipped);
            m_Seq.Clear();
            Assert.IsFalse(plan.Sync(m_Seq.Echoes), "a finished lesson doesn't come back after Clear");
            Assert.IsTrue(plan.Done);
        }

        [Test]
        public void TheTuneEndsWithOneTimedNoteOnGo()
        {
            var last = TutorialPlan.Song[TutorialPlan.Song.Length - 1];
            Assert.IsTrue(last.OnTime);
            Assert.AreEqual(1, TutorialPlan.Song.Count(s => s.OnTime), "only the last note is timed");
            Assert.AreEqual(12, last.BarStep, "Go is beat 4");
            Assert.AreEqual(InstrumentType.Erhu, last.Instrument, "the lamp sings \"lamb\"");
            Assert.AreEqual(2, last.GlowNote, "F# glows");
            Assert.IsTrue(TutorialPlan.Song.Any(s => s.Instrument == InstrumentType.Udu && s.BarStep == 12), "the drum plays on Go too");
        }

        [Test]
        public void TimingIsJudgedAroundGoAndForgivenAfterTwoMisses()
        {
            Assert.AreEqual(TutorialPlan.Timing.OnTime, TutorialPlan.Judge(12, 12, 0));
            Assert.AreEqual(TutorialPlan.Timing.OnTime, TutorialPlan.Judge(12, 10, 0), "two 16ths early still counts");
            Assert.AreEqual(TutorialPlan.Timing.OnTime, TutorialPlan.Judge(12, 14, 0));
            Assert.AreEqual(TutorialPlan.Timing.Early, TutorialPlan.Judge(12, 9, 0));
            Assert.AreEqual(TutorialPlan.Timing.Late, TutorialPlan.Judge(12, 15, 0));
            Assert.AreEqual(TutorialPlan.Timing.Late, TutorialPlan.Judge(12, 0, 0), "the next downbeat is late, around the bar");
            Assert.AreEqual(TutorialPlan.Timing.Early, TutorialPlan.Judge(12, 4, 0));
            Assert.AreEqual(TutorialPlan.Timing.OnTime, TutorialPlan.Judge(12, 4, TutorialPlan.ForgivingAfterMisses), "never required");
            Assert.AreEqual(-2, TutorialPlan.StepsFrom(12, 10));
            Assert.AreEqual(4, TutorialPlan.StepsFrom(12, 0));
        }

        [Test]
        public void TheCountIsThreeTwoOneGoOnTheFourBeats()
        {
            Assert.AreEqual(0, TutorialPlan.CountdownBeat(0.0));
            Assert.AreEqual(0, TutorialPlan.CountdownBeat(0.249));
            Assert.AreEqual(1, TutorialPlan.CountdownBeat(0.25));
            Assert.AreEqual(2, TutorialPlan.CountdownBeat(0.5));
            Assert.AreEqual(3, TutorialPlan.CountdownBeat(0.75), "Go on beat 4, where the note lands");
            Assert.AreEqual(3, TutorialPlan.CountdownBeat(0.999));
            Assert.AreEqual(12, MusicClock.StepsPerBar * 3 / 4);
        }

        [Test]
        public void SkipEndsTheLesson()
        {
            var plan = new TutorialPlan();
            plan.Skip();
            Assert.IsTrue(plan.Done && plan.Skipped);
            Assert.IsFalse(plan.Wants(InstrumentType.Mbira, 2));
        }

        [Test]
        public void TheWarmUpComesFirstAndTakesAnyNoteOnTheChair()
        {
            var plan = new TutorialPlan();
            Assert.IsTrue(plan.InWarmup, "the main mechanic before the song");
            Assert.IsFalse(plan.Wants(InstrumentType.Mbira, 2), "no song notes yet");
            Assert.IsFalse(plan.CompletesWarmup(InstrumentType.Erhu));
            Assert.IsTrue(plan.CompletesWarmup(InstrumentType.Mbira), "any chair note: a throw lands wherever it lands");
            Play(InstrumentType.Mbira, 2, snapTo: 0);
            Assert.IsFalse(plan.Sync(m_Seq.Echoes), "the song doesn't count during the warm-up");
            plan.FinishWarmup();
            Assert.IsTrue(plan.Wants(InstrumentType.Mbira, 2));
            var skipped = new TutorialPlan();
            skipped.Skip();
            Assert.IsFalse(skipped.InWarmup);
        }
    }
}
