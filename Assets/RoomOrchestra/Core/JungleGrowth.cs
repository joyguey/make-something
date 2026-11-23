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
        /// <summary>The toadstools (one entry each). Until 2026-11-22 the total notes at which each sprouted; now they
        /// follow <see cref="SongComplete"/>.</summary>
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

        /// <summary>Of the floor flowers, the share that ever grows (owner, headset 2026-11-22: "dial down the number of
        /// flowers and mushrooms that grow everywhere").</summary>
        public const float FlowerShare = 0.6f;

        /// <summary>
        /// How complete the three-instrument song is, 0 to 1: each of the three instruments counts fully once it has
        /// <see cref="NotesPerVine"/> notes (its ring's worth). Owner, 2026-11-22: "there should be a complete
        /// 3-instrument song before the room is filled": the floor fills with this, so one instrument alone, however
        /// many notes, grows a third of it.
        /// </summary>
        public float SongComplete
        {
            get
            {
                if (TotalNotes < NotesBeforeGrowth) return 0f;
                float sum = 0f;
                foreach (var type in new[] { InstrumentType.Udu, InstrumentType.Mbira, InstrumentType.Erhu })
                    sum += Math.Min(1f, m_Notes[(int)type] / (float)NotesPerVine);
                return sum / 3f;
            }
        }

        /// <summary>Floor flowers out of <paramref name="count"/>: the first with the growth, then as the
        /// three-instrument song completes, up to <see cref="FlowerShare"/> of them.</summary>
        public int FlowersSprouted(int count)
        {
            if (TotalNotes < NotesBeforeGrowth || count <= 0) return 0;
            int most = Math.Max(1, (int)Math.Round(count * FlowerShare));
            return Math.Max(1, Math.Min(most, (int)Math.Floor(most * SongComplete + 1e-4f)));
        }

        public bool AllVinesGrown =>
            VineProgress(InstrumentType.Udu) >= 1f && VineProgress(InstrumentType.Mbira) >= 1f && VineProgress(InstrumentType.Erhu) >= 1f;

        /// <summary>Toadstools out of <see cref="MushroomAt"/>'s count: the first with the growth, all of them only
        /// once the three-instrument song is complete (owner, 2026-11-22).</summary>
        public int MushroomsSprouted =>
            TotalNotes < NotesBeforeGrowth ? 0 : Math.Max(1, (int)Math.Floor(MushroomAt.Length * SongComplete + 1e-4f));

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
