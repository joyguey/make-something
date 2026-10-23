namespace RoomOrchestra
{
    /// <summary>How the player produced a hit. Every path ends in the same <see cref="MusicalHitEvent"/>.</summary>
    public enum InputPath
    {
        Toss,  // a thrown orb collided with a target
        Place, // select orb, then select target (tap, controller button, or editor key)
    }

    /// <summary>Dynamics as three discrete presets, so loudness never depends on physical strength.</summary>
    public enum IntensityTier
    {
        Quiet,
        Medium,
        Strong
    }

    /// <summary>
    /// Pitch request carried by a hit: either a normalized contact height (0 = bottom, 1 = top of the
    /// target) or one of five large petals. Phase 3 maps this to notes / udu articulations.
    /// </summary>
    public readonly struct PitchInput
    {
        public const int PetalCount = 5;

        /// <summary>Normalized height 0-1, or -1 when a petal was chosen.</summary>
        public float Height { get; }
        /// <summary>Petal 0-4, or -1 when height was used.</summary>
        public int Petal { get; }

        PitchInput(float height, int petal)
        {
            Height = height;
            Petal = petal;
        }

        public static PitchInput FromHeight(float normalized) =>
            new PitchInput(normalized < 0f ? 0f : normalized > 1f ? 1f : normalized, -1);

        public static PitchInput FromPetal(int petal) =>
            new PitchInput(-1f, petal < 0 ? 0 : petal >= PetalCount ? PetalCount - 1 : petal);

        public bool IsPetal => Petal >= 0;

        public override string ToString() => IsPetal ? $"petal {Petal}" : $"height {Height:F2}";
    }

    /// <summary>
    /// One normalized musical action. Toss and place both produce this, and everything downstream
    /// (sound, echoes, faces) consumes only this, so the two paths cannot drift apart.
    /// </summary>
    public sealed class MusicalHitEvent
    {
        /// <summary>Session-unique, monotonically increasing id (deterministic, unlike a GUID).</summary>
        public long StableId { get; }
        /// <summary>The intended action this hit came from (one throw, one place press).</summary>
        public long ActionId { get; }
        public InstrumentType Target { get; }
        public OrbType Orb { get; }
        public IntensityTier Tier { get; }
        public PitchInput Pitch { get; }
        public InputPath Path { get; }
        public double CreatedAtDspTime { get; }
        /// <summary>The note (or udu articulation) this hit plays: fully determined by target + pitch.</summary>
        public MusicalNote Note => NoteMapping.Resolve(Target, Pitch);

        public MusicalHitEvent(long stableId, long actionId, InstrumentType target, OrbType orb,
            IntensityTier tier, PitchInput pitch, InputPath path, double createdAtDspTime)
        {
            StableId = stableId;
            ActionId = actionId;
            Target = target;
            Orb = orb;
            Tier = tier;
            Pitch = pitch;
            Path = path;
            CreatedAtDspTime = createdAtDspTime;
        }

        /// <summary>True when both hits ask for the same music, regardless of input path, id or time.</summary>
        public bool IsSameIntent(MusicalHitEvent other) =>
            other != null && Target == other.Target && Orb == other.Orb && Tier == other.Tier &&
            Pitch.IsPetal == other.Pitch.IsPetal && Pitch.Petal == other.Pitch.Petal &&
            System.Math.Abs(Pitch.Height - other.Pitch.Height) < 1e-6f;

        public override string ToString() =>
            $"#{StableId} {Orb}->{Target} {Note} {Tier} ({Pitch}) via {Path} (action {ActionId})";
    }
}
