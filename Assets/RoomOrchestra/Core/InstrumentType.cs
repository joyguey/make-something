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
        /// <summary>The first three (the lesson's, and the furniture's own).</summary>
        public static readonly InstrumentType[] Original = { InstrumentType.Udu, InstrumentType.Mbira, InstrumentType.Erhu };
        /// <summary>The V4 library.</summary>
        public static readonly InstrumentType[] Library = { InstrumentType.Ocarina, InstrumentType.Handpan, InstrumentType.Guzheng, InstrumentType.Balafon, InstrumentType.SlitDrum };
        public static int Count => Original.Length + Library.Length;
        /// <summary>Held, sung notes (a bow or a breath) rather than struck ones.</summary>
        public static bool Sustained(InstrumentType type) => type == InstrumentType.Erhu || type == InstrumentType.Ocarina;
    }
}
