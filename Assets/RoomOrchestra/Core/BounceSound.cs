using System;

namespace RoomOrchestra
{
    /// <summary>
    /// A ball's own bounce off a wall (owner, V3 list: balls make their own sound when they bounce off walls): how
    /// loud and how bright, from the impact speed. Pure rules; the ball plays it. Floor landings stay silent and
    /// instrument hits play the instrument.
    /// </summary>
    public static class BounceSound
    {
        /// <summary>Softer bumps than this (m/s) make no sound; bounces closer together than this (s) sound once.</summary>
        public const float MinSpeed = 0.6f, GapSeconds = 0.08f;
        public const float QuietVolume = 0.25f, LoudVolume = 0.7f;
        /// <summary>The speed (m/s) at which a bounce is as loud as it gets.</summary>
        public const float LoudSpeed = 4.6f;

        public static float Volume(float speed)
        {
            if (speed < MinSpeed) return 0f;
            float u = Math.Min(1f, (speed - MinSpeed) / (LoudSpeed - MinSpeed));
            return QuietVolume + (LoudVolume - QuietVolume) * u;
        }

        /// <summary>A harder bounce is a touch brighter (within about a semitone), so repeats are never identical.</summary>
        public static float Pitch(float speed) => 0.97f + 0.06f * Math.Min(1f, Math.Max(0f, speed) / LoudSpeed);
    }
}
