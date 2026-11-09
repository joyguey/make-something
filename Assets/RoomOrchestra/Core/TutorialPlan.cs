using System;
using System.Collections.Generic;

namespace RoomOrchestra
{
    /// <summary>
    /// The guided first song (Version 1 feedback: "start with a single piece of furniture", "a tutorial song";
    /// owner, 2026-10-29: the chair first, the others appear in a big poof, Flutter talks and the next thing to
    /// tap glows). "Mary Had a Little Lamb" (public domain) fits the five notes (1 2 3 5 6 of D: D E F# A B)
    /// and, in eighth notes, one bar: the chair plays "Ma-ry had a" (F# E D E), the lamp "lit-tle lamb"
    /// (F# F#), the table adds a beat, and the last word, "lamb", is played in time: a rhythm-game count of
    /// 3, 2, 1, Go! (owner, 2026-10-29) on the bar's four beats, and the lamp's note lands on Go (beat 4).
    /// Every other correct note snaps to its place in the bar. The timed note has a generous window and, after a
    /// couple of misses, counts wherever it lands: timing is taught, never required (no fast reflexes). A tap or
    /// the controller's place counts like a throw. Pure rules; the director in the scene does the showing.
    /// </summary>
    public sealed class TutorialPlan
    {
        public const int AnyNote = -1;

        public readonly struct Step
        {
            public readonly InstrumentType Instrument;
            /// <summary>The tile index (0 = lowest), or <see cref="AnyNote"/>.</summary>
            public readonly int Note;
            /// <summary>Where the note goes in the 16-step bar.</summary>
            public readonly int BarStep;
            /// <summary>What Flutter says while this note is wanted.</summary>
            public readonly UiTerm Line;
            /// <summary>This step brings its instrument in (it appears in a poof first).</summary>
            public readonly bool Introduces;
            /// <summary>Played in time with the 3-2-1-Go count instead of snapped into place.</summary>
            public readonly bool OnTime;
            /// <summary>The tile that glows (the wanted note, or a middle one when any will do).</summary>
            public int GlowNote => Note == AnyNote ? 2 : Note;

            public Step(InstrumentType instrument, int note, int barStep, UiTerm line, bool introduces = false, bool onTime = false)
            {
                OnTime = onTime;
                Instrument = instrument;
                Note = note;
                BarStep = barStep;
                Line = line;
                Introduces = introduces;
            }
        }

        const int D = 0, E = 1, FSharp = 2;

        public static readonly Step[] Song =
        {
            new Step(InstrumentType.Mbira, FSharp, 0, UiTerm.TutorialHello),
            new Step(InstrumentType.Mbira, E, 2, UiTerm.TutorialMa),
            new Step(InstrumentType.Mbira, D, 4, UiTerm.TutorialMary),
            new Step(InstrumentType.Mbira, E, 6, UiTerm.TutorialMaryHad),
            new Step(InstrumentType.Erhu, FSharp, 8, UiTerm.TutorialLamp, introduces: true),
            new Step(InstrumentType.Erhu, FSharp, 10, UiTerm.TutorialLit),
            new Step(InstrumentType.Udu, AnyNote, 0, UiTerm.TutorialDrum, introduces: true),
            new Step(InstrumentType.Udu, AnyNote, 4, UiTerm.TutorialBeat2),
            new Step(InstrumentType.Udu, AnyNote, 8, UiTerm.TutorialBeat3),
            new Step(InstrumentType.Udu, AnyNote, 12, UiTerm.TutorialBeat4),
            // "lamb", on Go. Any of the lamp's notes counts (a thrown ball's pitch is the height it hits); F# glows.
            new Step(InstrumentType.Erhu, AnyNote, 12, UiTerm.TutorialOnGo, onTime: true),
        };

        /// <summary>How far from Go (in 16ths, about 0.17 s each at 88 BPM) a timed note still counts.</summary>
        public const int OnTimeWindowSteps = 2;
        /// <summary>After this many misses the timed note counts wherever it lands.</summary>
        public const int ForgivingAfterMisses = 2;

        /// <summary>Signed distance from <paramref name="wanted"/> to <paramref name="hit"/> around the bar: negative is early.</summary>
        public static int StepsFrom(int wanted, int hit)
        {
            int half = MusicClock.StepsPerBar / 2;
            return ((hit - wanted + MusicClock.StepsPerBar + half) % MusicClock.StepsPerBar + MusicClock.StepsPerBar) % MusicClock.StepsPerBar - half;
        }

        public enum Timing { OnTime, Early, Late }

        /// <summary>Is a timed note that landed on <paramref name="hitStep"/> on Go?</summary>
        public static Timing Judge(int wanted, int hitStep, int misses)
        {
            int d = StepsFrom(wanted, hitStep);
            if (misses >= ForgivingAfterMisses || Math.Abs(d) <= OnTimeWindowSteps) return Timing.OnTime;
            return d < 0 ? Timing.Early : Timing.Late;
        }

        /// <summary>Which count shows on this beat of the bar: 3, 2, 1, then Go! on beat 4 (where "lamb" lands).</summary>
        public static int CountdownBeat(double barPhase) =>
            Math.Min(MusicClock.BeatsPerBar - 1, Math.Max(0, (int)Math.Floor(barPhase * MusicClock.BeatsPerBar)));

        /// <summary>
        /// The warm-up before the song teaches the main mechanic (Version 1 feedback: "teach them the main mechanic
        /// before introducing additional items"): any note on the warm-up instrument, thrown or tapped. It is not part
        /// of the song: the director takes that note back and the song starts.
        /// </summary>
        public const InstrumentType WarmupInstrument = InstrumentType.Mbira;
        public bool WarmupDone { get; private set; }
        public bool InWarmup => !WarmupDone && !Done;

        /// <summary>Is a note just played the warm-up's (any note on the chair)?</summary>
        public bool CompletesWarmup(InstrumentType instrument) => InWarmup && instrument == WarmupInstrument;

        public void FinishWarmup() => WarmupDone = true;

        public int Index { get; private set; }
        /// <summary>The song is finished or skipped: from then on it stays out of the way (even after Clear).</summary>
        public bool Done { get; private set; }
        public bool Skipped { get; private set; }
        public Step Current => Song[Math.Min(Index, Song.Length - 1)];

        public static bool Matches(Step step, InstrumentType instrument, int noteIndex) =>
            instrument == step.Instrument && (step.Note == AnyNote || noteIndex == step.Note);

        /// <summary>Is a note just played the one asked for now?</summary>
        public bool Wants(InstrumentType instrument, int noteIndex) => !Done && !InWarmup && Matches(Song[Index], instrument, noteIndex);

        /// <summary>
        /// How many steps of the song, from the first, are in place in <paramref name="echoes"/>. Progress is
        /// read from the song itself, so Undo, Clear and Clear's undo all keep the lesson in step.
        /// </summary>
        public static int Progress(IReadOnlyList<EchoEvent> echoes)
        {
            int i = 0;
            for (; i < Song.Length; i++)
            {
                bool found = false;
                foreach (var echo in echoes)
                    if (echo.Step == Song[i].BarStep && Matches(Song[i], echo.Target, echo.NoteIndex)) { found = true; break; }
                if (!found) break;
            }
            return i;
        }

        /// <summary>Follows the song; true if the step changed. Reaching the end finishes the lesson.</summary>
        public bool Sync(IReadOnlyList<EchoEvent> echoes)
        {
            if (Done || InWarmup) return false;
            int progress = Progress(echoes);
            if (progress == Index) return false;
            Index = progress;
            if (Index >= Song.Length) Done = true;
            return true;
        }

        /// <summary>Has <paramref name="instrument"/> joined by step <paramref name="index"/>? (Until then it is hidden.)</summary>
        public static bool IntroducedBy(InstrumentType instrument, int index)
        {
            for (int i = 0; i < Song.Length; i++)
                if (Song[i].Instrument == instrument) return i <= index;
            return true;
        }

        public void Skip()
        {
            WarmupDone = true;
            Skipped = true;
            Done = true;
        }
    }
}
