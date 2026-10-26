using System;
using System.Collections.Generic;

namespace RoomOrchestra
{
    /// <summary>
    /// How the room turns into a jungle as music is composed. Every note the player ADDS to the song
    /// feeds its instrument's vine and the forest floor; repeats of the loop do not, so nothing keeps
    /// growing unless the song does (owner). Butterflies follow the song as it stands now (layered
    /// instruments, varied notes); the chorus needs everything at once.
    /// The scale is pentatonic, so any combination is consonant: "harmonious and developed" here
    /// means layered (several instruments) with melodic variety, sustained over time.
    /// Clearing the song clears the jungle too (in a cloud of smoke): each song grows its own forest.
    /// </summary>
    public sealed class JungleGrowth
    {
        /// <summary>Notes added on one instrument that grow its vine to full length (a full ring holds 6).</summary>
        public const int NotesPerVine = 6;
        /// <summary>Notes added on every instrument together that grow the ball-stand vine.</summary>
        public const int NotesForStandVine = 16;
        /// <summary>Total notes added at which each toadstool sprouts, in order.</summary>
        public static readonly int[] MushroomAt = { 1, 3, 5, 7, 9, 12, 15 };

        readonly int[] m_Notes = new int[3];

        public int TotalNotes { get; private set; }

        public void NoteAdded(InstrumentType instrument)
        {
            m_Notes[(int)instrument]++;
            TotalNotes++;
        }

        public int NotesOn(InstrumentType instrument) => m_Notes[(int)instrument];

        public float VineProgress(InstrumentType instrument) => Math.Min(1f, m_Notes[(int)instrument] / (float)NotesPerVine);

        public float StandVineProgress => Math.Min(1f, TotalNotes / (float)NotesForStandVine);

        public bool AllVinesGrown =>
            VineProgress(InstrumentType.Udu) >= 1f && VineProgress(InstrumentType.Mbira) >= 1f && VineProgress(InstrumentType.Erhu) >= 1f;

        public int MushroomsSprouted
        {
            get
            {
                int n = 0;
                while (n < MushroomAt.Length && TotalNotes >= MushroomAt[n]) n++;
                return n;
            }
        }

        /// <summary>
        /// Butterflies out for the song as it stands: one once two instruments play together, two with
        /// all three and a fuller loop, three when the loop is rich and melodic (4+ different notes).
        /// </summary>
        public static int ButterfliesFor(IReadOnlyList<EchoEvent> loop)
        {
            if (loop == null || loop.Count == 0) return 0;
            var instruments = new HashSet<InstrumentType>();
            var notes = new HashSet<(InstrumentType, int)>();
            foreach (var e in loop)
            {
                instruments.Add(e.Target);
                notes.Add((e.Target, e.NoteIndex));
            }
            if (instruments.Count >= 3 && loop.Count >= 10 && notes.Count >= 4) return 3;
            if (instruments.Count >= 3 && loop.Count >= 6) return 2;
            if (instruments.Count >= 2 && loop.Count >= 4) return 1;
            return 0;
        }

        /// <summary>The finale: every vine grown and all three butterflies out.</summary>
        public bool Chorus(int butterfliesOut) => AllVinesGrown && butterfliesOut >= 3;
    }
}
