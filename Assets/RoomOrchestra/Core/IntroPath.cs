using System;

namespace RoomOrchestra
{
    /// <summary>
    /// The opening animation's motion (owner, 2026-10-29): the leaf palette glides in on a swoop like a
    /// falling leaf, then the three characters bounce up from the floor and land on it, in front of the
    /// player. Positions are in the leaf pad's space (the seated layout: seat at the origin, facing +Z),
    /// relative to the leaf's home; the leaf starts beyond the edge of a seated player's view.
    /// Pure maths, so it is tested without the engine.
    /// </summary>
    public static class IntroPath
    {
        public const float SwoopSeconds = 2.6f;
        /// <summary>Time between one character's take-off and the next's.</summary>
        public const float HopStagger = 0.3f;
        /// <summary>Two little bounces on the floor, then the leap onto the leaf.</summary>
        public static readonly float[] BounceSeconds = { 0.22f, 0.26f, 0.5f };
        static readonly float[] BounceHeights = { 0.06f, 0.1f, 0.22f };

        // A cubic Bezier from just behind and above the player's head (owner, headset 2026-11-08: "appear behind the
        // player's head and float down"), over the right shoulder, across in front, dipping left and low, home.
        static readonly (float x, float y, float z)[] Swoop =
        {
            (0.5f, 1.45f, -2.1f),
            (1.6f, 1.5f, 1.6f),
            (-0.7f, 0.5f, 0.9f),
            (0f, 0f, 0f),
        };

        /// <summary>Length of the whole opening for three characters.</summary>
        public static float TotalSeconds(int characters) =>
            SwoopSeconds + HopStagger * Math.Max(0, characters - 1) + BounceSeconds[0] + BounceSeconds[1] + BounceSeconds[2];

        /// <summary>The leaf's offset from home and its tilt (degrees) <paramref name="seconds"/> into the swoop.</summary>
        public static (float x, float y, float z, float pitch, float yaw, float roll) LeafAt(float seconds)
        {
            float t = Clamp01(seconds / SwoopSeconds);
            float u = t * t * (3f - 2f * t); // gentle start and a soft landing
            float a = 1f - u;
            float b0 = a * a * a, b1 = 3f * a * a * u, b2 = 3f * a * u * u, b3 = u * u * u;
            float x = b0 * Swoop[0].x + b1 * Swoop[1].x + b2 * Swoop[2].x + b3 * Swoop[3].x;
            float y = b0 * Swoop[0].y + b1 * Swoop[1].y + b2 * Swoop[2].y + b3 * Swoop[3].y;
            float z = b0 * Swoop[0].z + b1 * Swoop[1].z + b2 * Swoop[2].z + b3 * Swoop[3].z;
            // Rocks side to side like a falling leaf, the swings dying away as it settles.
            float roll = (float)Math.Sin(u * Math.PI * 3.0) * 25f * a;
            float pitch = (float)Math.Sin(u * Math.PI) * 12f;
            float yaw = -70f * a;
            return (x, y, z, pitch, yaw, roll);
        }

        /// <summary>
        /// Where a character is <paramref name="seconds"/> after its take-off, as a blend from its floor spot
        /// (0) to its place on the leaf (1) plus a lift (metres) above that straight line. Done after
        /// <see cref="BounceSeconds"/> in total.
        /// </summary>
        public static (float blend, float lift, bool landed) HopAt(float seconds)
        {
            if (seconds <= 0f) return (0f, 0f, false);
            float start = 0f;
            for (int i = 0; i < BounceSeconds.Length; i++)
            {
                float end = start + BounceSeconds[i];
                if (seconds < end)
                {
                    float t = (seconds - start) / BounceSeconds[i];
                    float lift = 4f * BounceHeights[i] * t * (1f - t); // a parabola
                    bool leap = i == BounceSeconds.Length - 1;
                    return (leap ? t : 0f, lift, false);
                }
                start = end;
            }
            return (1f, 0f, true);
        }

        /// <summary>The bounce index (0, 1 = on the floor, 2 = the leap) at <paramref name="seconds"/>, or -1 when landed.</summary>
        public static int BounceIndex(float seconds)
        {
            float start = 0f;
            for (int i = 0; i < BounceSeconds.Length; i++)
            {
                start += BounceSeconds[i];
                if (seconds < start) return i;
            }
            return -1;
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
