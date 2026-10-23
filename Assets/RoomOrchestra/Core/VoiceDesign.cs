using System;

namespace RoomOrchestra
{
    /// <summary>
    /// How to reshape a source sample into one playable voice. All processing is offline and
    /// deterministic (same input, same samples), and never changes the note's pitch except for the
    /// udu Bend articulation, which is a percussion gesture rather than a melody note.
    /// </summary>
    public struct VoiceSpec
    {
        /// <summary>Playback rate at the start and end of the bend (1 = original pitch).</summary>
        public float RateStart, RateEnd;
        public float BendSeconds;
        /// <summary>Output length in seconds (0 = natural length of the source).</summary>
        public float LengthSeconds;
        /// <summary>Linear fade to silence at the end, so cut voices never click.</summary>
        public float FadeSeconds;
        /// <summary>One-pole low-pass cutoff in Hz (0 = off): darker, rounder.</summary>
        public float LowPassHz;
        /// <summary>0-1: adds the signal's own high-frequency edge: brighter, airier.</summary>
        public float Brightness;
        /// <summary>Peak level after normalising (keeps headroom; never above 0.9).</summary>
        public float PeakLevel;

        public static VoiceSpec Plain => new VoiceSpec { RateStart = 1f, RateEnd = 1f, PeakLevel = 0.8f, FadeSeconds = 0.05f };
    }

    /// <summary>Timbre rules: orb weight and udu articulation become <see cref="VoiceSpec"/>s.</summary>
    public static class VoiceDesign
    {
        public const float MaxPeak = 0.9f;

        /// <summary>
        /// Flutter: airy, bright, shorter. Bop: centred, plucked, the plain sample.
        /// Boom: rounder, darker, longer. (No sub-octave layer: it shifted Boom's heard pitch an
        /// octave down, and pitch must come only from height/petal.)
        /// </summary>
        public static VoiceSpec ForOrb(OrbType orb, VoiceSpec basis)
        {
            var spec = basis;
            switch (orb)
            {
                case OrbType.Flutter:
                    spec.Brightness = Math.Min(1f, spec.Brightness + 0.6f);
                    spec.LengthSeconds = spec.LengthSeconds > 0f ? spec.LengthSeconds * 0.7f : 0.7f;
                    spec.PeakLevel *= 0.9f;
                    break;
                case OrbType.Boom:
                    spec.LowPassHz = spec.LowPassHz > 0f ? Math.Min(spec.LowPassHz, 900f) : 900f;
                    spec.FadeSeconds = Math.Max(spec.FadeSeconds, 0.15f);
                    break;
            }
            return spec;
        }

        /// <summary>The five udu articulations, built from the low (D2) and high (A2/B2) udu samples.</summary>
        public static VoiceSpec ForUdu(UduArticulation articulation)
        {
            var spec = VoiceSpec.Plain;
            switch (articulation)
            {
                case UduArticulation.Muted:
                    spec.LengthSeconds = 0.18f;
                    spec.FadeSeconds = 0.08f;
                    spec.LowPassHz = 600f;
                    break;
                case UduArticulation.Slap:
                    spec.LengthSeconds = 0.25f;
                    spec.FadeSeconds = 0.1f;
                    spec.Brightness = 0.7f;
                    break;
                case UduArticulation.Bend:
                    spec.RateStart = 1.15f;
                    spec.RateEnd = 1f;
                    spec.BendSeconds = 0.3f;
                    break;
                case UduArticulation.Tap:
                    spec.LengthSeconds = 0.1f;
                    spec.FadeSeconds = 0.05f;
                    spec.Brightness = 0.8f;
                    spec.PeakLevel = 0.55f;
                    break;
            }
            return spec;
        }

        /// <summary>Which of the 5 udu source samples (D2 E2 F#2 A2 B2) each articulation starts from.</summary>
        public static int UduSourceIndex(UduArticulation articulation) => articulation switch
        {
            UduArticulation.Slap => 3,
            UduArticulation.Tap => 4,
            _ => 0,
        };

        /// <summary>Renders <paramref name="source"/> (mono) into a new voice following <paramref name="spec"/>.</summary>
        public static float[] Render(float[] source, int sampleRate, VoiceSpec spec)
        {
            if (source == null || source.Length == 0 || sampleRate <= 0) return Array.Empty<float>();

            float[] main = Resample(source, sampleRate, spec);

            if (spec.LowPassHz > 0f) LowPass(main, sampleRate, spec.LowPassHz);
            if (spec.Brightness > 0f) Brighten(main, sampleRate, spec.Brightness);

            int length = spec.LengthSeconds > 0f ? Math.Min(main.Length, (int)(spec.LengthSeconds * sampleRate)) : main.Length;
            var output = new float[length];
            Array.Copy(main, output, length);

            FadeOut(output, Math.Max(1, (int)(spec.FadeSeconds * sampleRate)));
            Normalize(output, Math.Min(MaxPeak, spec.PeakLevel > 0f ? spec.PeakLevel : 0.8f));
            return output;
        }

        // Variable-rate linear-interpolation resampler; rate glides from RateStart to RateEnd.
        static float[] Resample(float[] source, int sampleRate, VoiceSpec spec)
        {
            float rateStart = spec.RateStart > 0f ? spec.RateStart : 1f;
            float rateEnd = spec.RateEnd > 0f ? spec.RateEnd : rateStart;
            int bendSamples = Math.Max(1, (int)(spec.BendSeconds * sampleRate));
            var output = new System.Collections.Generic.List<float>(source.Length * 2);
            double position = 0;
            int n = 0;
            while (position < source.Length - 1)
            {
                int i = (int)position;
                float frac = (float)(position - i);
                output.Add(source[i] + (source[i + 1] - source[i]) * frac);
                float t = Math.Min(1f, n / (float)bendSamples);
                position += rateStart + (rateEnd - rateStart) * t;
                n++;
            }
            return output.ToArray();
        }

        // Two cascaded one-pole stages (12 dB/octave): a one-pole filter barely darkened Boom
        // (only 1.17x darker than Bop in tests); two poles at 900 Hz give about 1.4-1.6x across the
        // udu, mbira and erhu registers while staying above the erhu's top notes.
        static void LowPass(float[] samples, int sampleRate, float cutoffHz)
        {
            double rc = 1.0 / (2.0 * Math.PI * cutoffHz);
            double dt = 1.0 / sampleRate;
            float alpha = (float)(dt / (rc + dt));
            for (int pass = 0; pass < 2; pass++)
            {
                float y = 0f;
                for (int i = 0; i < samples.Length; i++)
                {
                    y += alpha * (samples[i] - y);
                    samples[i] = y;
                }
            }
        }

        // High-shelf boost: content above ~1 kHz is raised by up to (1 + 3 * amount), i.e. about +9 dB at
        // amount 0.6, emphasising the attack and upper partials without touching the fundamental's pitch.
        static void Brighten(float[] samples, int sampleRate, float amount)
        {
            const float shelfHz = 1000f;
            double rc = 1.0 / (2.0 * Math.PI * shelfHz);
            float alpha = (float)((1.0 / sampleRate) / (rc + 1.0 / sampleRate));
            float low = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                low += alpha * (samples[i] - low);
                samples[i] += (samples[i] - low) * amount * 3f;
            }
        }

        static void FadeOut(float[] samples, int fadeSamples)
        {
            int start = Math.Max(0, samples.Length - fadeSamples);
            int span = samples.Length - start;
            for (int i = start; i < samples.Length; i++)
                samples[i] *= (samples.Length - 1 - i) / (float)Math.Max(1, span - 1);
        }

        static void Normalize(float[] samples, float peak)
        {
            float max = 0f;
            foreach (float s in samples) max = Math.Max(max, Math.Abs(s));
            if (max < 1e-6f) return;
            float gain = peak / max;
            for (int i = 0; i < samples.Length; i++) samples[i] *= gain;
        }

        /// <summary>Mean absolute first difference relative to RMS: a simple, deterministic brightness measure.</summary>
        public static float BrightnessMeasure(float[] samples)
        {
            if (samples == null || samples.Length < 2) return 0f;
            double diff = 0, energy = 0;
            for (int i = 1; i < samples.Length; i++)
            {
                diff += Math.Abs(samples[i] - samples[i - 1]);
                energy += samples[i] * samples[i];
            }
            double rms = Math.Sqrt(energy / samples.Length);
            return rms < 1e-9 ? 0f : (float)(diff / samples.Length / rms);
        }
    }
}
