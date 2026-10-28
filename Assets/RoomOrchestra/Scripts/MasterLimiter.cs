using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Puts <see cref="PeakLimiter"/> on the final mix (this sits on the AudioListener), so even the
    /// densest song (three full rings ringing at once) never clips the output. Inaudible in normal play;
    /// logs how hard it had to work, at most once a bar, so a device run shows whether it engaged.
    /// </summary>
    [RequireComponent(typeof(AudioListener))]
    public class MasterLimiter : MonoBehaviour
    {
        PeakLimiter m_Limiter;
        float m_NextReport;
        int m_Callbacks;

        public int Callbacks => m_Callbacks;

        void Awake() => m_Limiter = new PeakLimiter(AudioSettings.outputSampleRate);

        void OnAudioFilterRead(float[] data, int channels)
        {
            m_Callbacks++;
            m_Limiter?.Process(data, channels);
        }

        void Update()
        {
            if (m_Limiter == null || Time.unscaledTime < m_NextReport) return;
            m_NextReport = Time.unscaledTime + 2.7f;
            float reduction = m_Limiter.TakeMaxReductionDb();
            if (reduction > 0.1f) Debug.Log($"[RoomOrchestra] Mix limiter held a loud moment down by {reduction:F1} dB");
        }
    }
}
