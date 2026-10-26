namespace RoomOrchestra
{
    /// <summary>
    /// How each character moves (docs/ART_AND_AUDIO_BIBLE.md): Flutter light and floaty with a quick
    /// flutter, Bop springy and elastic, Boom slow, heavy and warm. Springs are per second; amounts are
    /// fractions of the body size; angles in degrees.
    /// </summary>
    public readonly struct CharacterMotion
    {
        /// <summary>Idle breaths per second and how much the body swells (squash/stretch fraction).</summary>
        public float BreatheRate { get; }
        public float BreatheAmount { get; }
        /// <summary>Idle side-to-side sway (degrees) and its own slower rate.</summary>
        public float SwayDegrees { get; }
        /// <summary>Spring stiffness (higher = snappier) and damping ratio (lower = wobblier) of the jelly.</summary>
        public float Stiffness { get; }
        public float Damping { get; }
        /// <summary>How far the jelly leans from acceleration (degrees per m/s²) and from speed (degrees per m/s).</summary>
        public float LeanPerAccel { get; }
        public float LeanPerSpeed { get; }
        /// <summary>Hop height (body heights) and duration (s).</summary>
        public float HopHeight { get; }
        public float HopSeconds { get; }

        public CharacterMotion(float breatheRate, float breatheAmount, float swayDegrees, float stiffness, float damping,
            float leanPerAccel, float leanPerSpeed, float hopHeight, float hopSeconds)
        {
            BreatheRate = breatheRate;
            BreatheAmount = breatheAmount;
            SwayDegrees = swayDegrees;
            Stiffness = stiffness;
            Damping = damping;
            LeanPerAccel = leanPerAccel;
            LeanPerSpeed = leanPerSpeed;
            HopHeight = hopHeight;
            HopSeconds = hopSeconds;
        }

        public const float MaxLeanDegrees = 28f;
        public const float MaxSquash = 0.35f;

        public static CharacterMotion For(OrbType type) => type switch
        {
            OrbType.Flutter => new CharacterMotion(breatheRate: 0.9f, breatheAmount: 0.05f, swayDegrees: 6f,
                stiffness: 90f, damping: 0.18f, leanPerAccel: 1.4f, leanPerSpeed: 10f, hopHeight: 0.6f, hopSeconds: 0.45f),
            OrbType.Bop => new CharacterMotion(breatheRate: 0.7f, breatheAmount: 0.06f, swayDegrees: 4f,
                stiffness: 160f, damping: 0.12f, leanPerAccel: 1.1f, leanPerSpeed: 8f, hopHeight: 0.45f, hopSeconds: 0.35f),
            _ => new CharacterMotion(breatheRate: 0.4f, breatheAmount: 0.07f, swayDegrees: 2.5f,
                stiffness: 45f, damping: 0.3f, leanPerAccel: 0.8f, leanPerSpeed: 5f, hopHeight: 0.25f, hopSeconds: 0.5f),
        };

        /// <summary>Hop height at <paramref name="t"/> (0..1 of the hop): a single arc, 0 at both ends.</summary>
        public static float HopArc(float t) => t <= 0f || t >= 1f ? 0f : 4f * t * (1f - t);
    }
}
