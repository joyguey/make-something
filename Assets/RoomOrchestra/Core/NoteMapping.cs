namespace RoomOrchestra
{
    /// <summary>Udu sounds: percussion articulations, deliberately not a pitched keyboard.</summary>
    public enum UduArticulation
    {
        Open,  // low "bloop" from the side hole
        Muted, // palm-damped, short
        Slap,  // sharp hand slap on the body
        Bend,  // hand over the hole: the pitch sinks as it rings
        Tap    // light fingertip tap
    }

    /// <summary>What one hit plays: a scale degree on mbira/erhu, or an articulation on udu.</summary>
    public readonly struct MusicalNote
    {
        public InstrumentType Instrument { get; }
        /// <summary>0-4: scale degree (D E F# A B) or <see cref="UduArticulation"/>.</summary>
        public int Index { get; }
        public bool IsPercussion => Instrument == InstrumentType.Udu;
        public UduArticulation Articulation => (UduArticulation)Index;

        public MusicalNote(InstrumentType instrument, int index)
        {
            Instrument = instrument;
            Index = index;
        }

        /// <summary>Readable name, e.g. "F#4" or "udu Bend".</summary>
        public string Label => IsPercussion
            ? $"udu {Articulation}"
            : NoteMapping.NoteName(Index) + (Instrument == InstrumentType.Mbira ? "4" : "5");

        public override string ToString() => Label;
    }

    /// <summary>
    /// Deterministic note grammar (docs/INTERACTION_AND_MUSIC_SYSTEM.md): five equal height bands
    /// or five petals choose index 0-4. The same input always gives the same note.
    /// D major pentatonic is this game's arrangement, not a claim about traditional mbira tuning.
    /// </summary>
    public static class NoteMapping
    {
        public const int Count = 5;
        static readonly string[] s_Names = { "D", "E", "F#", "A", "B" };

        /// <summary>noteIndex = clamp(floor(h * 5), 0, 4); the top edge h = 1 maps to 4.</summary>
        public static int IndexFromHeight(float normalizedHeight)
        {
            int index = (int)System.Math.Floor(normalizedHeight * Count);
            return index < 0 ? 0 : index >= Count ? Count - 1 : index;
        }

        public static int IndexFor(PitchInput pitch) => pitch.IsPetal ? pitch.Petal : IndexFromHeight(pitch.Height);

        public static MusicalNote Resolve(InstrumentType instrument, PitchInput pitch) =>
            new MusicalNote(instrument, IndexFor(pitch));

        static readonly float[] s_Degrees4 = { 293.66f, 329.63f, 369.99f, 440f, 493.88f }; // D4 E4 F#4 A4 B4

        /// <summary>
        /// Pitch of the source sample for instrument + sample index: mbira D4-B4, erhu one octave up,
        /// udu two octaves down (D2-B2). Used to place timbre filters relative to the note.
        /// </summary>
        public static float FrequencyHz(InstrumentType instrument, int sampleIndex)
        {
            float f = s_Degrees4[sampleIndex < 0 ? 0 : sampleIndex >= Count ? Count - 1 : sampleIndex];
            return instrument switch
            {
                InstrumentType.Erhu => f * 2f,
                InstrumentType.Udu => f / 4f,
                _ => f,
            };
        }

        public static string NoteName(int index) => s_Names[index < 0 ? 0 : index >= Count ? Count - 1 : index];
    }
}
