namespace RoomOrchestra
{
    /// <summary>
    /// Instruments that can be assigned to a musical object, tuned to D major pentatonic.
    /// Values are serialized by index, so append new instruments rather than reordering.
    /// </summary>
    public enum InstrumentType
    {
        Udu,   // Igbo clay pot drum: bass "bloop" on root D
        Mbira, // Shona thumb piano: plucked metal tines
        Erhu,  // Chinese two-string bowed fiddle: sustained melody
        // The V4 library (owner, 2026-11-11: "5 more instruments that may sound harmonious together, include the
        // ocarina and other lesser-known instruments"), all on D major pentatonic like the first three.
        Ocarina,  // clay vessel flute: pure, breathy melody (D5-B5)
        Handpan,  // tuned steel hand drum: bell-like, long ring (D4-B4)
        Guzheng,  // Chinese plucked zither: bright pluck with a press-bend (D4-B4)
        Balafon,  // West African wooden xylophone with gourd buzz (D4-B4)
        SlitDrum  // hollow log drum: warm, woody, pitched (D3-B3)
    }

    public static class Instruments
    {
        /// <summary>The furniture's own three (the lesson's): the udu on the table, the handpan on the chair, the erhu on
        /// the lamp (owner, 2026-11-26: the handpan replaces the mbira in both modes; the mbira stays in the library).</summary>
        public static readonly InstrumentType[] Original = { InstrumentType.Udu, InstrumentType.Handpan, InstrumentType.Erhu };
        /// <summary>The library: every other instrument, chosen with Change.</summary>
        public static readonly InstrumentType[] Library = { InstrumentType.Ocarina, InstrumentType.Mbira, InstrumentType.Guzheng, InstrumentType.Balafon, InstrumentType.SlitDrum };
        /// <summary>The chair's instrument (the middle of <see cref="Original"/>).</summary>
        public const InstrumentType Chair = InstrumentType.Handpan;
        public static int Count => Original.Length + Library.Length;
        /// <summary>Held, sung notes (a bow or a breath) rather than struck ones.</summary>
        public static bool Sustained(InstrumentType type) => type == InstrumentType.Erhu || type == InstrumentType.Ocarina;
    }
}
