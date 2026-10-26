using System.Collections.Generic;

namespace RoomOrchestra
{
    /// <summary>The three Little Echoes characters. Serialized by index, so append rather than reorder.</summary>
    public enum OrbType
    {
        Flutter, // tall turquoise, sparkle motif, light
        Bop,     // round lavender, single dot motif, medium
        Boom     // wide amber, ripple motif, heavy
    }

    /// <summary>
    /// Data for one orb character: how it looks without relying on color, and its virtual weight.
    /// Weight is an affordance (mass, bounce, speed clamp); no orb needs a harder throw to work.
    /// Gravity and drag are identical for all orbs so aim assist lands every orb on its target.
    /// </summary>
    public sealed class OrbProfile
    {
        public OrbType Type { get; }
        public string DisplayName { get; }
        /// <summary>Shape cue that identifies the orb in grayscale.</summary>
        public string Silhouette { get; }
        /// <summary>Symbol cue (sparkle / dot / ripple) shown on the orb and its tray token.</summary>
        public string Motif { get; }
        public float Mass { get; }
        public float Bounciness { get; }
        /// <summary>Launch speeds are clamped to this (m/s) so no orb flies dangerously fast.</summary>
        public float MaxLaunchSpeed { get; }
        /// <summary>Body proportions relative to a unit ball (width, height, depth).</summary>
        public float ScaleX { get; }
        public float ScaleY { get; }
        public float ScaleZ { get; }
        /// <summary>Base color as 0-1 RGB; never the only distinguishing cue.</summary>
        public float R { get; }
        public float G { get; }
        public float B { get; }

        public OrbProfile(OrbType type, string displayName, string silhouette, string motif,
            float mass, float bounciness, float maxLaunchSpeed,
            float scaleX, float scaleY, float scaleZ, float r, float g, float b)
        {
            Type = type;
            DisplayName = displayName;
            Silhouette = silhouette;
            Motif = motif;
            Mass = mass;
            Bounciness = bounciness;
            MaxLaunchSpeed = maxLaunchSpeed;
            ScaleX = scaleX;
            ScaleY = scaleY;
            ScaleZ = scaleZ;
            R = r;
            G = g;
            B = b;
        }
    }

    /// <summary>
    /// The fixed set of orb profiles. Values follow docs/INTERACTION_AND_MUSIC_SYSTEM.md; body colours are
    /// sampled from the owner-approved reference (docs/reference/Flutter_Bop_Boom_USER_APPROVED_REFERENCE.png).
    /// </summary>
    public static class OrbCatalog
    {
        static readonly OrbProfile[] s_All =
        {
            new OrbProfile(OrbType.Flutter, "Flutter", "tall droplet", "sparkle",
                mass: 0.6f, bounciness: 0.35f, maxLaunchSpeed: 9f,
                scaleX: 0.85f, scaleY: 1.25f, scaleZ: 0.85f, r: 0.61f, g: 0.89f, b: 0.90f),
            new OrbProfile(OrbType.Bop, "Bop", "round orb", "dot",
                mass: 1.0f, bounciness: 0.6f, maxLaunchSpeed: 9f,
                scaleX: 1f, scaleY: 1f, scaleZ: 1f, r: 0.76f, g: 0.61f, b: 0.95f),
            new OrbProfile(OrbType.Boom, "Boom", "wide dumpling", "ripple",
                mass: 1.6f, bounciness: 0.15f, maxLaunchSpeed: 9f,
                scaleX: 1.3f, scaleY: 0.8f, scaleZ: 1.3f, r: 0.91f, g: 0.73f, b: 0.42f),
        };

        public static IReadOnlyList<OrbProfile> All => s_All;
        public static int Count => s_All.Length;

        public static OrbProfile Get(OrbType type) => s_All[(int)type];
    }
}
