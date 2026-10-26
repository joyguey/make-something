using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// A floating rainforest sparkle near an instrument: it bobs and twinkles gently on its own (slow,
    /// smooth size changes, never a flash), and flares when its instrument plays, live or as an echo
    /// passing the top of its ring. Starts a few seconds after load; reduced motion holds it still.
    /// </summary>
    public class SparkleTwinkle : MonoBehaviour
    {
        public const float FlareSeconds = 0.5f, FlareSize = 1.8f;

        public static bool ReducedMotion { get; set; }

        [SerializeField] InstrumentTarget instrument;
        [SerializeField] float phase;

        Vector3 m_Home, m_Scale;
        bool m_Captured;
        float m_FlareStart = -10f;

        public InstrumentTarget Instrument => instrument;
        public bool Flaring => Time.time - m_FlareStart < FlareSeconds;

        public void Configure(InstrumentTarget target, float startPhase)
        {
            instrument = target;
            phase = startPhase;
        }

        void OnEnable()
        {
            MusicalHitPipeline.Committed += OnHit;
            OrbitView.NotePassed += OnNote;
        }

        void OnDisable()
        {
            MusicalHitPipeline.Committed -= OnHit;
            OrbitView.NotePassed -= OnNote;
        }

        void OnHit(MusicalHitEvent hit)
        {
            if (instrument != null && hit.Target == instrument.Instrument) Flare();
        }

        void OnNote(InstrumentTarget target)
        {
            if (target == instrument) Flare();
        }

        public void Flare()
        {
            Capture();
            if (!ReducedMotion) m_FlareStart = Time.time;
        }

        void Capture()
        {
            if (m_Captured) return;
            m_Captured = true;
            m_Home = transform.localPosition;
            m_Scale = transform.localScale;
        }

        void Update()
        {
            if (ReducedMotion || Time.timeSinceLevelLoad < JellyBody.StartDelaySeconds) return;
            Capture();
            float t = Time.time + phase * 10f;
            float flareT = (Time.time - m_FlareStart) / FlareSeconds;
            float flare = flareT >= 0f && flareT < 1f ? Mathf.Sin(flareT * Mathf.PI) * (FlareSize - 1f) : 0f;
            float twinkle = 0.85f + 0.15f * Mathf.Sin(t * 2.2f);
            transform.localPosition = m_Home + Vector3.up * (Mathf.Sin(t * 0.8f) * 0.03f);
            transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.5f) * 20f + flare * 90f);
            transform.localScale = m_Scale * (twinkle + flare);
        }
    }
}
