using System;

namespace RoomOrchestra
{
    /// <summary>Story mode's own sounds (placeholders rendered by maths, nothing to license).</summary>
    public enum StorySound
    {
        SqueakCall,  // a Squeak comes through: a quick high chirp up and down
        WobbleCall,  // a Wobbler: a low warble
        ClangCall,   // a Clang: a dull metal clank
        Sour,        // the wrong friend: two notes a semitone apart, rubbing
        Launch,      // a friend flicked: a soft airy swoosh
        Shell,       // the Grumble's shell breaks: a deep hollow crack and a bright ring
        Chorus,      // the Chorus: a rising D major pentatonic run
        Escape,      // a Discord got into the room: a falling, detuned slide
        Beat,        // the level's pulse when no drum is in the room: a soft low thump
        Win,         // the level won: a bright D major arpeggio
        Lose,        // the level lost: a slow falling minor third
    }

    /// <summary>
    /// Renders <see cref="StorySound"/>s: short, soft, mono, ending in silence. The friendly ones are on D major
    /// pentatonic like the rest of the game; the Discords' and the sour note are deliberately off. Tested in EditMode.
    /// </summary>
    public static class StorySoundSynth
    {
        const float D3 = 146.83f, A3 = 220f, D4 = 293.66f, E4 = 329.63f, Fs4 = 369.99f, A4 = 440f, B4 = 493.88f, D5 = 587.33f, Fs5 = 739.99f, A5 = 880f, D6 = 1174.66f;

        public static float Level(StorySound s) => s switch
        {
            StorySound.Beat => 0.45f,
            StorySound.Launch => 0.25f,
            StorySound.Sour => 0.32f,
            StorySound.Escape => 0.3f,
            _ => 0.35f,
        };

        public static float[] Render(StorySound s, int rate) => s switch
        {
            StorySound.SqueakCall => Glide(1500f, 2400f, 1300f, 0.16f, rate, square: 0.15f),
            StorySound.WobbleCall => Warble(160f, 9f, 0.35f, rate),
            StorySound.ClangCall => Clank(rate),
            StorySound.Sour => Notes(new[] { 466.16f, 493.88f }, 0f, 0.35f, rate, together: true),
            StorySound.Launch => Swoosh(rate),
            StorySound.Shell => Mix(Thump(70f, 0.25f, rate), Notes(new[] { D6 }, 0f, 0.3f, rate)),
            StorySound.Chorus => Notes(new[] { D4, E4, Fs4, A4, B4, D5, Fs5, A5 }, 0.05f, 0.25f, rate),
            StorySound.Escape => Glide(330f, 300f, 180f, 0.45f, rate, square: 0.3f),
            StorySound.Beat => Thump(D3 * 0.5f, 0.18f, rate),
            StorySound.Win => Notes(new[] { D4, Fs4, A4, D5, Fs5, A5, D6 }, 0.09f, 0.4f, rate),
            StorySound.Lose => Notes(new[] { A3, Fs4 * 0.5f * 1.0f, D3 }, 0.28f, 0.45f, rate),
            _ => new float[1],
        };

        static float[] Glide(float from, float peak, float to, float seconds, int rate, float square)
        {
            int n = (int)(seconds * rate);
            var d = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float hz = t < 0.35f ? Lerp(from, peak, t / 0.35f) : Lerp(peak, to, (t - 0.35f) / 0.65f);
                phase += 2 * Math.PI * hz / rate;
                float s = (float)Math.Sin(phase);
                float sq = s >= 0 ? 1f : -1f;
                float env = Math.Min(1f, i / (0.005f * rate)) * (1f - t);
                d[i] = (s * (1f - square) + sq * square) * env;
            }
            return Finish(d);
        }

        static float[] Warble(float hz, float wobble, float seconds, int rate)
        {
            int n = (int)(seconds * rate);
            var d = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float f = hz * (1f + 0.12f * (float)Math.Sin(2 * Math.PI * wobble * t));
                phase += 2 * Math.PI * f / rate;
                float env = Math.Min(1f, t / 0.02f) * (float)Math.Exp(-t / 0.15f);
                d[i] = ((float)Math.Sin(phase) + 0.3f * (float)Math.Sin(phase * 2.01)) * env;
            }
            return Finish(d);
        }

        static float[] Clank(int rate)
        {
            // inharmonic partials (a bell gone wrong), quick decay
            float[] f = { 310f, 457f, 723f, 1093f };
            float[] a = { 1f, 0.6f, 0.4f, 0.25f };
            int n = (int)(0.4f * rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float v = 0f;
                for (int k = 0; k < f.Length; k++) v += a[k] * (float)Math.Sin(2 * Math.PI * f[k] * t) * (float)Math.Exp(-t / (0.12f / (k + 1)));
                d[i] = v * Math.Min(1f, t / 0.002f);
            }
            return Finish(d);
        }

        static float[] Swoosh(int rate)
        {
            int n = (int)(0.22f * rate);
            var d = new float[n];
            var random = new Random(7);
            float low = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float noise = (float)(random.NextDouble() * 2 - 1);
                float k = 0.05f + 0.3f * t; // brightening as it goes
                low += (noise - low) * k;
                d[i] = low * (float)Math.Sin(Math.PI * t);
            }
            return Finish(d);
        }

        static float[] Thump(float hz, float seconds, int rate)
        {
            int n = (int)(seconds * rate);
            var d = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float f = hz * (1f + 1.5f * (float)Math.Exp(-t / 0.02f));
                phase += 2 * Math.PI * f / rate;
                d[i] = (float)Math.Sin(phase) * (float)Math.Exp(-t / (seconds * 0.3f)) * Math.Min(1f, t / 0.002f);
            }
            return Finish(d);
        }

        static float[] Notes(float[] hz, float gap, float decay, int rate, bool together = false)
        {
            float length = (together ? 0f : gap * (hz.Length - 1)) + decay * 4f;
            int n = (int)(length * rate);
            var d = new float[n];
            for (int k = 0; k < hz.Length; k++)
            {
                int start = together ? 0 : (int)(k * gap * rate);
                for (int i = start; i < n; i++)
                {
                    float t = (i - start) / (float)rate;
                    float env = Math.Min(1f, t / 0.004f) * (float)Math.Exp(-t / decay);
                    d[i] += env * ((float)Math.Sin(2 * Math.PI * hz[k] * t) + 0.2f * (float)Math.Sin(4 * Math.PI * hz[k] * t));
                }
            }
            return Finish(d);
        }

        static float[] Mix(float[] a, float[] b)
        {
            var d = new float[Math.Max(a.Length, b.Length)];
            for (int i = 0; i < d.Length; i++) d[i] = (i < a.Length ? a[i] : 0f) + (i < b.Length ? b[i] : 0f);
            return Finish(d);
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * Math.Max(0f, Math.Min(1f, t));

        // peak to 0.9, and a short fade so it ends in silence
        static float[] Finish(float[] d)
        {
            float peak = 0f;
            foreach (var v in d) peak = Math.Max(peak, Math.Abs(v));
            float g = peak > 0f ? 0.9f / peak : 0f;
            int fade = Math.Min(d.Length, 256);
            for (int i = 0; i < d.Length; i++)
            {
                d[i] *= g;
                int fromEnd = d.Length - 1 - i;
                if (fromEnd < fade) d[i] *= fromEnd / (float)fade;
            }
            return d;
        }
    }
}
