using System;
using System.Collections.Generic;

namespace RoomOrchestra
{
    /// <summary>
    /// How the room turns into a jungle as music is composed. Every note heard (a live hit or an echo
    /// coming round) feeds its instrument's vine and the forest floor; butterflies follow the song as
    /// it stands now (layered instruments, varied notes); the chorus needs everything at once.
    /// The scale is pentatonic, so any combination is consonant: "harmonious and developed" here
    /// means layered (several instruments) with melodic variety, sustained over time.
    /// Growth is kept for the session (clearing a song does not uproot the forest); butterflies leave
    /// when the song they followed is cleared.
    /// </summary>
    public sealed class JungleGrowth
    {
        /// <summary>Notes on one instrument that grow its vine from bud to full length.</summary>
        public const int NotesPerVine = 32;
        /// <summary>Notes from every instrument together that grow the ball-stand vine.</summary>
        public const int NotesForStandVine = 80;
        /// <summary>Total notes at which each toadstool sprouts, in order.</summary>
        public static readonly int[] MushroomAt = { 6, 14, 24, 36, 50, 66, 85 };

        readonly int[] m_Notes = new int[3];

        public int TotalNotes { get; private set; }

        public void NotePlayed(InstrumentType instrument)
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
