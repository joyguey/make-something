namespace RoomOrchestra
{
    /// <summary>
    /// Maps impact speed to one of three dynamics tiers, and tiers to playback strength.
    /// <see cref="Sensitivity"/> above 1 lets gentle throws reach louder tiers (low-strength players).
    /// </summary>
    public sealed class IntensityMapping
    {
        public const float DefaultMediumFrom = 2.5f; // m/s
        public const float DefaultStrongFrom = 5f;   // m/s

        public float MediumFrom { get; }
        public float StrongFrom { get; }
        public float Sensitivity { get; set; } = 1f;

        public IntensityMapping(float mediumFrom = DefaultMediumFrom, float strongFrom = DefaultStrongFrom)
        {
            MediumFrom = mediumFrom;
            StrongFrom = strongFrom < mediumFrom ? mediumFrom : strongFrom;
        }

        /// <summary>Thresholds are inclusive lower bounds: exactly <see cref="MediumFrom"/> is Medium.</summary>
        public IntensityTier FromImpactSpeed(float speed)
        {
            float s = speed * (Sensitivity > 0f ? Sensitivity : 1f);
            if (s >= StrongFrom) return IntensityTier.Strong;
            if (s >= MediumFrom) return IntensityTier.Medium;
            return IntensityTier.Quiet;
        }

        /// <summary>Playback strength (0-1) for a tier; quiet stays clearly audible.</summary>
        public static float Strength(IntensityTier tier) => tier switch
        {
            IntensityTier.Quiet => 0.45f,
            IntensityTier.Medium => 0.7f,
            _ => 1f,
        };
    }
}
