using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Something that grows on the forest floor (a <see cref="Toadstool"/> or a <see cref="FloorFlower"/>).
    /// It waits as a little bud, sprouts to full size when the jungle has heard enough music, then sways
    /// to the beat and bounces on every beat (a hop on every beat in the chorus). Beat comes from the
    /// master clock, so it moves in time with the music, never with the frame rate. Motion starts a few
    /// seconds after load; reduced motion keeps it still.
    /// </summary>
    public abstract class FloorSprout : MonoBehaviour
    {
        public const float BudScale = 0.25f, SproutSeconds = 1.1f;
        const float SwayDegrees = 9f, BeatSquash = 0.14f, ChorusHop = 0.06f;

        public static bool ReducedMotion { get; set; }

        [Tooltip("The look that sways and squashes; the root keeps the bud/grown size.")]
        [SerializeField] Transform visual;
        [SerializeField] float fullScale = 1f;

        float m_SproutStart = -1f;
        Vector3 m_VisualHome;
        bool m_Captured;

        public bool Sprouted => m_SproutStart >= 0f;
        public bool Chorus { get; set; }
        public Transform Visual => visual;
        public float FullScale => fullScale;

        public void Configure(Transform look, float grownSize)
        {
            visual = look;
            fullScale = grownSize;
        }

        public void Sprout()
        {
            if (Sprouted) return;
            m_SproutStart = Time.time;
            Debug.Log($"[RoomOrchestra] Jungle: {name} sprouts");
        }

        /// <summary>Back to a bud at once (cleared, under the smoke).</summary>
        public void Unsprout()
        {
            m_SproutStart = -1f;
            Chorus = false;
            transform.localScale = Vector3.one * BudScale;
            if (visual != null && m_Captured)
            {
                visual.localPosition = m_VisualHome;
                visual.localRotation = Quaternion.identity;
                visual.localScale = Vector3.one;
            }
        }

        void Update()
        {
            if (Time.timeSinceLevelLoad < JellyBody.StartDelaySeconds || visual == null) return;
            if (!m_Captured)
            {
                m_Captured = true;
                m_VisualHome = visual.localPosition;
            }

            float grow = Sprouted ? (ReducedMotion ? 1f : Mathf.Clamp01((Time.time - m_SproutStart) / SproutSeconds)) : 0f;
            // Springy sprout: overshoots a touch, then settles.
            float popped = grow <= 0f ? 0f : 1f + Mathf.Sin(grow * Mathf.PI * 1.5f) * (1f - grow) * 0.35f;
            var size = Vector3.one * Mathf.Lerp(BudScale, fullScale, Mathf.Clamp01(grow)) * (grow > 0f && grow < 1f ? popped : 1f);
            if (transform.localScale != size) transform.localScale = size;

            if (!Sprouted || ReducedMotion) return;
            var conductor = EchoConductor.Instance;
            if (conductor == null || conductor.IsPaused) return;
            double bar = conductor.Clock.BarPhase(AudioSettings.dspTime);
            double beat = bar * MusicClock.BeatsPerBar;
            float sinceBeat = (float)(beat - System.Math.Floor(beat)); // 0 right on the beat
            float squash = Mathf.Pow(1f - sinceBeat, 3f) * BeatSquash;
            float sway = Mathf.Sin((float)(bar * 2.0 * System.Math.PI * 2.0)) * SwayDegrees; // left-right every two beats
            float hop = Chorus ? Mathf.Sin(Mathf.Clamp01(sinceBeat * 2f) * Mathf.PI) * ChorusHop : 0f;
            visual.localScale = new Vector3(1f + squash * 0.6f, 1f - squash, 1f + squash * 0.6f);
            visual.localRotation = Quaternion.Euler(0f, 0f, sway);
            visual.localPosition = m_VisualHome + Vector3.up * hop;
        }
    }
}
