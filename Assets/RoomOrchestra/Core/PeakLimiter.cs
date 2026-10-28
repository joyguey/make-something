using System;

namespace RoomOrchestra
{
    /// <summary>
    /// Safety limiter for the final mix: below the threshold it does nothing (gain exactly 1); when
    /// the summed music would go over, the whole mix is turned down just enough at once, then eased
    /// back up over the release time. Linked across channels so the stereo image never shifts. The
    /// envelope never falls below the current sample, so the output can never exceed the threshold.
    /// </summary>
    public sealed class PeakLimiter
    {
        public const float DefaultThreshold = 0.9f;
        public const float DefaultReleaseSeconds = 0.12f;

        readonly float m_Threshold;
        readonly float m_Release;
        float m_Envelope;

        /// <summary>Largest gain reduction since the last read (dB, positive), for diagnostics.</summary>
        public float MaxReductionDb { get; private set; }

        public PeakLimiter(int sampleRate, float threshold = DefaultThreshold, float releaseSeconds = DefaultReleaseSeconds)
        {
            m_Threshold = threshold;
            m_Release = (float)Math.Exp(-1.0 / (Math.Max(1, sampleRate) * releaseSeconds));
        }

        /// <summary>Limits interleaved <paramref name="data"/> in place.</summary>
        public void Process(float[] data, int channels)
        {
            if (data == null || channels < 1) return;
            for (int i = 0; i + channels <= data.Length; i += channels)
            {
                float peak = 0f;
                for (int c = 0; c < channels; c++) peak = Math.Max(peak, Math.Abs(data[i + c]));
                m_Envelope = Math.Max(peak, m_Envelope * m_Release);
                if (m_Envelope <= m_Threshold) continue;
                float gain = m_Threshold / m_Envelope;
                for (int c = 0; c < channels; c++) data[i + c] *= gain;
                float reduction = -20f * (float)Math.Log10(gain);
                if (reduction > MaxReductionDb) MaxReductionDb = reduction;
            }
        }

        public float TakeMaxReductionDb()
        {
            float r = MaxReductionDb;
            MaxReductionDb = 0f;
            return r;
        }
    }
}
