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

        // ---- The bounce chime (owner, headset 2026-11-08: "Bouncing the balls around the wall should add another
        // aesthetically pleasing sound that harmonizes with the rest of the soundscape") ----

        /// <summary>
        /// A ball's bounces in one throw climb the song's own chord, D F# A B D (all in D major pentatonic, the scale
        /// every instrument plays), so any bounce fits whatever is playing.
        /// </summary>
        public static readonly int[] ChimeSemitones = { 0, 4, 7, 9, 12 };
        /// <summary>The chime is this share of the bounce's own volume: a sparkle on top, never louder than the music.</summary>
        public const float ChimeLevel = 0.45f;
        public const float ChimeSeconds = 1.1f;

        /// <summary>The D each character's chime starts from (Hz): light Flutter high, heavy Boom low.</summary>
        public static float ChimeRootHz(OrbType orb) => orb switch
        {
            OrbType.Flutter => 1174.66f, // D6
            OrbType.Bop => 587.33f,      // D5
            _ => 293.66f                 // D4
        };

        /// <summary>The <paramref name="bounce"/>th bounce of a throw (0 = first): its note (Hz), climbing and starting over.</summary>
        public static float ChimeHz(OrbType orb, int bounce)
        {
            int step = ChimeSemitones[((bounce % ChimeSemitones.Length) + ChimeSemitones.Length) % ChimeSemitones.Length];
            return ChimeRootHz(orb) * (float)Math.Pow(2.0, step / 12.0);
        }

        /// <summary>
        /// A soft bell (synthesised, so nothing to license): a few harmonic partials, each fading, the higher ones
        /// faster, with a 4 ms start so it never clicks. Peak about 0.8.
        /// </summary>
        public static float[] RenderChime(float hz, int sampleRate)
        {
            int n = (int)(ChimeSeconds * sampleRate);
            var data = new float[n];
            float[] partials = { 1f, 2f, 3f, 4.01f };
            float[] levels = { 1f, 0.35f, 0.14f, 0.06f };
            float[] decay = { 0.42f, 0.25f, 0.16f, 0.1f }; // seconds to fall to 1/e
            float attack = 0.004f * sampleRate;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sampleRate;
                float v = 0f;
                for (int p = 0; p < partials.Length; p++)
                {
                    float f = hz * partials[p];
                    if (f >= sampleRate * 0.45f) continue; // nothing above what the output can carry
                    v += levels[p] * (float)Math.Exp(-t / decay[p]) * (float)Math.Sin(2.0 * Math.PI * f * t);
                }
                float env = i < attack ? i / attack : 1f;
                float tail = i > n - 256 ? (n - i) / 256f : 1f; // ends at silence
                data[i] = 0.55f * v * env * tail;
            }
            return data;
        }
    }
}
