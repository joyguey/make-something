using System;

namespace RoomOrchestra
{
    /// <summary>The interface's own sounds (V3 review: "Needs UI sound effects").</summary>
    public enum UiSound { Hover, Press, Open, Close, Confirm, Cancel, Place }

    /// <summary>
    /// Synthesises the interface sounds: short, soft and in the song's key (D major pentatonic, like the bounce
    /// chimes), so a tap never clashes with the music. An octave lower and shorter than the first set (owner, Fable
    /// refinement 2026-11-14, option A "lower and softer": the high bells read as a music box). Pure maths, one mono buffer per sound; tests check each is short,
    /// below full scale and ends at silence. Rendered once at startup (no audio files to license).
    /// </summary>
    public static class UiSoundSynth
    {
        // D4, A4, then D5, F#5, A5, D6, F#6, A6, D7
        public const float D4 = 293.66f, A4 = 440f;
        public const float D5 = 587.33f, Fs5 = 739.99f, A5 = 880f, D6 = 1174.66f, Fs6 = 1479.98f, A6 = 1760f, D7 = 2349.32f;

        /// <summary>Each sound's level (0..1) before the player's mix.</summary>
        public static float Level(UiSound sound) => sound switch
        {
            UiSound.Hover => 0.08f,
            UiSound.Press => 0.40f,
            UiSound.Open or UiSound.Close => 0.28f,
            UiSound.Confirm or UiSound.Place => 0.30f,
            _ => 0.25f,
        };

        public static float[] Render(UiSound sound, int sampleRate) => sound switch
        {
            UiSound.Hover => Bell(new[] { A5 }, 0.012f, 0.035f, sampleRate, partials: 1),
            UiSound.Press => Pop(sampleRate),
            UiSound.Open => Bell(new[] { D5, A5 }, 0.06f, 0.126f, sampleRate),
            UiSound.Close => Bell(new[] { A5, D5 }, 0.06f, 0.126f, sampleRate),
            UiSound.Confirm => Bell(new[] { D5, Fs5, A5 }, 0.055f, 0.196f, sampleRate),
            UiSound.Cancel => Bell(new[] { A4, D4 }, 0.07f, 0.14f, sampleRate),
            UiSound.Place => Bell(new[] { D5, A5, D6 }, 0.05f, 0.245f, sampleRate),
            _ => new float[1],
        };

        // A soft wooden pop: a sine falling an octave from 660 Hz in 60 ms, with a tiny click at the start.
        static float[] Pop(int sampleRate)
        {
            int n = (int)(0.09f * sampleRate);
            var data = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sampleRate;
                float hz = 660f * (float)Math.Pow(0.5, Math.Min(1.0, t / 0.06));
                phase += 2.0 * Math.PI * hz / sampleRate;
                float env = (float)Math.Exp(-t / 0.025);
                float click = i < 24 ? (1f - i / 24f) * 0.3f : 0f;
                data[i] = (float)Math.Sin(phase) * env * 0.9f + click;
            }
            return Finish(data);
        }

        // Notes one after another (gap seconds apart), each a small bell decaying with 'decay' seconds.
        static float[] Bell(float[] notes, float gap, float decay, int sampleRate, int partials = 3)
        {
            float length = gap * (notes.Length - 1) + decay * 4f;
            int n = (int)(length * sampleRate);
            var data = new float[n];
            float[] ratio = { 1f, 2f, 3f };
            float[] level = { 1f, 0.25f, 0.08f };
            for (int k = 0; k < notes.Length; k++)
            {
                int start = (int)(k * gap * sampleRate);
                for (int i = start; i < n; i++)
                {
                    float t = (i - start) / (float)sampleRate;
                    float attack = Math.Min(1f, t / 0.003f);
                    float env = attack * (float)Math.Exp(-t / decay);
                    float v = 0f;
                    for (int p = 0; p < partials && p < ratio.Length; p++)
                    {
                        float f = notes[k] * ratio[p];
                        if (f >= sampleRate * 0.45f) continue;
                        v += level[p] * (float)Math.Sin(2.0 * Math.PI * f * t);
                    }
                    data[i] += v * env / notes.Length * 1.4f;
                }
            }
            return Finish(data);
        }

        // Below full scale and fading to silence at the end (no click when it stops).
        static float[] Finish(float[] data)
        {
            float peak = 0f;
            foreach (var v in data) peak = Math.Max(peak, Math.Abs(v));
            float gain = peak > 0.9f ? 0.9f / peak : 1f;
            int fade = Math.Min(256, data.Length);
            for (int i = 0; i < data.Length; i++)
            {
                float tail = i > data.Length - fade ? (data.Length - i) / (float)fade : 1f;
                data[i] *= gain * tail;
            }
            return data;
        }
    }
}
