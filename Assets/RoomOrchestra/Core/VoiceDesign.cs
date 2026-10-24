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
        /// <summary>Skip this much of the source first (e.g. a bowed note's slow swell).</summary>
        public float StartSeconds;
        /// <summary>Output length in seconds before any echo tail (0 = natural length).</summary>
        public float LengthSeconds;
        /// <summary>Linear fade-in: a soft edge instead of a hard attack.</summary>
        public float AttackSeconds;
        /// <summary>Exponential decay time constant (0 = keep the sample's own decay).</summary>
        public float DecaySeconds;
        /// <summary>Linear fade to silence at the very end, so voices never click.</summary>
        public float FadeSeconds;
        /// <summary>Soft saturation (tanh) drive; adds overtones even to near-pure tones (0 = off).</summary>
        public float Drive;
        /// <summary>Two-pole low-pass cutoff in Hz (0 = off): darker, rounder.</summary>
        public float LowPassHz;
        /// <summary>High-shelf corner in Hz and amount 0-1: content above it is raised up to (1 + 3 x amount).</summary>
        public float ShelfHz, Brightness;
        /// <summary>0-1: level of three fading echoes (0.15 / 0.30 / 0.45 s) that lengthen the voice.</summary>
        public float EchoMix;
        /// <summary>Peak level after normalising (keeps headroom; never above 0.9).</summary>
        public float PeakLevel;

        public static VoiceSpec Plain => new VoiceSpec { RateStart = 1f, RateEnd = 1f, PeakLevel = 0.8f, FadeSeconds = 0.05f };
    }

    /// <summary>
    /// Timbre rules: orb weight and udu articulation become <see cref="VoiceSpec"/>s.
    /// The placeholder samples are close to pure tones, so orb identity rests mainly on shape in
    /// time (which survives any sample) and only secondarily on tone colour:
    /// Flutter = short light blip, Bop = the plain plucked note, Boom = dark, soft-edged, echoing.
    /// </summary>
    public static class VoiceDesign
    {
        public const float MaxPeak = 0.9f;
        static readonly float[] s_EchoTimes = { 0.15f, 0.30f, 0.45f };
        static readonly float[] s_EchoGains = { 1f, 0.7f, 0.5f };

        /// <param name="fundamentalHz">Pitch of the source sample: filters are placed relative to it.</param>
        /// <param name="bowed">Bowed voices (erhu) swell slowly; Flutter starts from the swell instead.</param>
        public static VoiceSpec ForOrb(OrbType orb, VoiceSpec basis, float fundamentalHz, bool bowed)
        {
            var spec = basis;
            float f0 = fundamentalHz > 0f ? fundamentalHz : 440f;
            switch (orb)
            {
                case OrbType.Flutter:
                    spec.Drive = Math.Max(spec.Drive, 3f);
                    spec.ShelfHz = Math.Max(2f * f0, 600f);
                    spec.Brightness = Math.Min(1f, spec.Brightness + 0.6f);
                    spec.DecaySeconds = 0.12f;
                    spec.LengthSeconds = spec.LengthSeconds > 0f ? Math.Min(spec.LengthSeconds, 0.5f) : 0.5f;
                    if (bowed)
                    {
                        spec.StartSeconds = 0.4f;
                        spec.AttackSeconds = Math.Max(spec.AttackSeconds, 0.01f);
                    }
                    spec.PeakLevel *= 0.9f;
                    break;
                case OrbType.Boom:
                    float cutoff = Math.Max(1.5f * f0, 150f);
                    spec.LowPassHz = spec.LowPassHz > 0f ? Math.Min(spec.LowPassHz, cutoff) : cutoff;
                    spec.AttackSeconds = Math.Max(spec.AttackSeconds, 0.04f);
                    spec.EchoMix = 0.5f;
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
                    spec.LowPassHz = 300f;
                    break;
                case UduArticulation.Slap:
                    spec.LengthSeconds = 0.25f;
                    spec.FadeSeconds = 0.1f;
                    spec.Drive = 2f;
                    spec.ShelfHz = 600f;
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
                    spec.Drive = 2f;
                    spec.ShelfHz = 600f;
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

            // Never skip more than half the source: a short sample must still produce a voice.
            int skip = Math.Min(source.Length / 2, (int)(spec.StartSeconds * sampleRate));
            var trimmed = new float[source.Length - skip];
            Array.Copy(source, skip, trimmed, 0, trimmed.Length);
            Normalize(trimmed, 1f); // drive behaves the same whatever the sample's recorded level

            float[] main = Resample(trimmed, sampleRate, spec);
            if (spec.Drive > 0f) Saturate(main, spec.Drive);
            if (spec.LowPassHz > 0f) LowPass(main, sampleRate, spec.LowPassHz);
            if (spec.Brightness > 0f) Shelf(main, sampleRate, spec.ShelfHz > 0f ? spec.ShelfHz : 1000f, spec.Brightness);

            int length = spec.LengthSeconds > 0f ? Math.Min(main.Length, (int)(spec.LengthSeconds * sampleRate)) : main.Length;
            var shaped = new float[length];
            Array.Copy(main, shaped, length);
            Envelope(shaped, sampleRate, spec.AttackSeconds, spec.DecaySeconds);

            float[] output = spec.EchoMix > 0f ? Echo(shaped, sampleRate, spec.EchoMix) : shaped;
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

        static void Saturate(float[] samples, float drive)
        {
            float norm = (float)Math.Tanh(drive);
            for (int i = 0; i < samples.Length; i++)
                samples[i] = (float)Math.Tanh(drive * samples[i]) / norm;
        }

        // Two cascaded one-pole stages (12 dB/octave).
        static void LowPass(float[] samples, int sampleRate, float cutoffHz)
        {
            float alpha = OnePoleAlpha(sampleRate, cutoffHz);
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

        static void Shelf(float[] samples, int sampleRate, float cornerHz, float amount)
        {
            float alpha = OnePoleAlpha(sampleRate, cornerHz);
            float low = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                low += alpha * (samples[i] - low);
                samples[i] += (samples[i] - low) * amount * 3f;
            }
        }

        static float OnePoleAlpha(int sampleRate, float cutoffHz)
        {
            double rc = 1.0 / (2.0 * Math.PI * cutoffHz);
            double dt = 1.0 / sampleRate;
            return (float)(dt / (rc + dt));
        }

        static void Envelope(float[] samples, int sampleRate, float attackSeconds, float decaySeconds)
        {
            int attack = (int)(attackSeconds * sampleRate);
            double decayPerSample = decaySeconds > 0f ? Math.Exp(-1.0 / (decaySeconds * sampleRate)) : 1.0;
            double gain = 1.0;
            for (int i = 0; i < samples.Length; i++)
            {
                float a = attack > 0 && i < attack ? i / (float)attack : 1f;
                samples[i] *= (float)(a * gain);
                gain *= decayPerSample;
            }
        }

        static float[] Echo(float[] samples, int sampleRate, float mix)
        {
            int longest = (int)(s_EchoTimes[s_EchoTimes.Length - 1] * sampleRate);
            var output = new float[samples.Length + longest];
            Array.Copy(samples, output, samples.Length);
            for (int e = 0; e < s_EchoTimes.Length; e++)
            {
                int offset = (int)(s_EchoTimes[e] * sampleRate);
                float gain = mix * s_EchoGains[e];
                for (int i = 0; i < samples.Length; i++) output[i + offset] += samples[i] * gain;
            }
            return output;
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

        /// <summary>
        /// Share of energy in harmonics 2-10 versus harmonics 1-10, measured with a Hann-windowed
        /// Goertzel analysis on the loudest 100 ms. A real brightness measure for harmonic sounds.
        /// </summary>
        public static float OvertoneFraction(float[] samples, int sampleRate, float fundamentalHz)
        {
            int window = sampleRate / 10;
            if (samples == null || samples.Length < window || fundamentalHz <= 0f) return 0f;
            int best = 0;
            double bestEnergy = -1;
            for (int start = 0; start + window <= samples.Length; start += window / 2)
            {
                double energy = 0;
                for (int i = start; i < start + window; i += 4) energy += samples[i] * samples[i];
                if (energy > bestEnergy) { bestEnergy = energy; best = start; }
            }
            double fundamental = 0, overtones = 0;
            for (int k = 1; k <= 10 && fundamentalHz * k < sampleRate / 2f; k++)
            {
                double power = Goertzel(samples, best, window, sampleRate, fundamentalHz * k);
                if (k == 1) fundamental = power; else overtones += power;
            }
            double total = fundamental + overtones;
            return total <= 0 ? 0f : (float)(overtones / total);
        }

        static double Goertzel(float[] samples, int start, int length, int sampleRate, double frequency)
        {
            double w = 2 * Math.PI * frequency / sampleRate, c = 2 * Math.Cos(w), s1 = 0, s2 = 0;
            for (int i = 0; i < length; i++)
            {
                double hann = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (length - 1));
                double s0 = samples[start + i] * hann + c * s1 - s2;
                s2 = s1;
                s1 = s0;
            }
            return Math.Max(0, s1 * s1 + s2 * s2 - c * s1 * s2);
        }
    }
}
