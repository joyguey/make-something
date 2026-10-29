using System;

namespace RoomOrchestra
{
    /// <summary>
    /// How a toadstool or flower comes up and falls into step (owner, 2026-10-29): it grows out of nothing
    /// (its waiting state is invisible), each at its own pace, and starts its sway a little off the beat, then
    /// eases into time with the others over a few seconds. Pure maths; the randomness is passed in.
    /// </summary>
    public static class SproutSync
    {
        /// <summary>Scale while waiting: too small to see, and not zero (PICO Spatial drops zero-size objects).</summary>
        public const float HiddenScale = 0.008f;
        /// <summary>A few sprouting together don't start at once.</summary>
        public const float MaxStartDelay = 0.6f;
        public const float MinGrowSeconds = 0.8f, MaxGrowSeconds = 1.8f;
        /// <summary>The largest beat offset a new sprout starts with (half a beat either way).</summary>
        public const float MaxOffsetBeats = 0.5f;
        /// <summary>How quickly the offset fades: under 4% left after <see cref="SettleSeconds"/>.</summary>
        public const float SettleTimeConstant = 1.2f;
        public const float SettleSeconds = 4f;

        public static float StartDelay(float random01) => Clamp01(random01) * MaxStartDelay;

        public static float GrowSeconds(float random01) => MinGrowSeconds + Clamp01(random01) * (MaxGrowSeconds - MinGrowSeconds);

        public static float InitialOffsetBeats(float random01) => (Clamp01(random01) * 2f - 1f) * MaxOffsetBeats;

        /// <summary>The offset left <paramref name="secondsSinceGrown"/> after it finished growing (all of it before).</summary>
        public static float OffsetBeats(float initialBeats, float secondsSinceGrown) =>
            secondsSinceGrown <= 0f ? initialBeats : initialBeats * (float)Math.Exp(-secondsSinceGrown / SettleTimeConstant);

        /// <summary>
        /// Growth from the waiting speck (0) to full size (1) <paramref name="seconds"/> after it was told to
        /// sprout, with a springy overshoot near the end.
        /// </summary>
        public static float Size(float seconds, float startDelay, float growSeconds)
        {
            float t = Clamp01((seconds - startDelay) / growSeconds);
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            float eased = t * t * (3f - 2f * t);
            return eased + (float)Math.Sin(t * Math.PI) * 0.25f; // pops a little past full, then settles
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
