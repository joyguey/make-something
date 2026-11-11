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
    /// Nothing grows until the song has a few notes (owner): then the vines catch up with every note so far,
    /// the first toadstool sprouts, and a flower pops up somewhere in the room with each new note.
    /// </summary>
    public sealed class JungleGrowth
    {
        /// <summary>Notes added on one instrument that grow its vine to full length (a full ring holds 6).</summary>
        public const int NotesPerVine = 6;
        /// <summary>Notes the song needs before anything grows.</summary>
        public const int NotesBeforeGrowth = 3;
        /// <summary>Total notes added at which each toadstool sprouts, in order.</summary>
        public static readonly int[] MushroomAt = { 3, 4, 6, 8, 10, 13, 16 };

        readonly int[] m_Notes = new int[System.Enum.GetValues(typeof(InstrumentType)).Length];

        public int TotalNotes { get; private set; }

        public void NoteAdded(InstrumentType instrument)
        {
            m_Notes[(int)instrument]++;
            TotalNotes++;
        }

        public int NotesOn(InstrumentType instrument) => m_Notes[(int)instrument];

        public float VineProgress(InstrumentType instrument) =>
            TotalNotes < NotesBeforeGrowth ? 0f : Math.Min(1f, m_Notes[(int)instrument] / (float)NotesPerVine);

        /// <summary>Floor flowers out of <paramref name="count"/>: the first with the growth, one more each note.</summary>
        public int FlowersSprouted(int count) => Math.Max(0, Math.Min(count, TotalNotes - NotesBeforeGrowth + 1));

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
