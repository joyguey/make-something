using System;

namespace RoomOrchestra
{
    /// <summary>
    /// The little jiggle a button, tile or character gives when a press lands (headset feedback, George 2026-11-03:
    /// "a slight jiggle on a successful press would tell me my input landed"). A quick swell and a few decaying wobbles,
    /// over in about a third of a second. Pure numbers: <see cref="PressJiggle"/> applies them to a transform.
    /// </summary>
    public static class PressJiggleCurve
    {
        public const float Duration = 0.35f;
        /// <summary>Peak wobble, in degrees about the line of sight: noticeable, never dizzying.</summary>
        public const float MaxAngle = 7f;
        /// <summary>Peak swell: 8 % bigger at the press, settling back.</summary>
        public const float MaxSwell = 0.08f;
        const float Wobbles = 2.5f;

        /// <summary>Rotation in degrees, <paramref name="t"/> seconds after the press; 0 before and after.</summary>
        public static float Angle(float t)
        {
            if (t <= 0f || t >= Duration) return 0f;
            float u = t / Duration;
            return MaxAngle * Decay(u) * (float)Math.Sin(u * Wobbles * 2.0 * Math.PI);
        }

        /// <summary>Scale multiplier minus one (0.08 = 8 % bigger); 0 before and after.</summary>
        public static float Swell(float t)
        {
            if (t <= 0f || t >= Duration) return 0f;
            float u = t / Duration;
            return MaxSwell * (float)Math.Sin(Math.PI * Math.Min(1.0, u * 2.0)) * Decay(u);
        }

        static float Decay(float u) => (1f - u) * (1f - u);
    }
}
