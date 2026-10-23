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
        Erhu   // Chinese two-string bowed fiddle: sustained melody
    }
}
