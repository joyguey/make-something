using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Something that grows on the forest floor (a <see cref="Toadstool"/> or a <see cref="FloorFlower"/>).
    /// It waits unseen (a speck, <see cref="SproutSync.HiddenScale"/>), grows up out of the ground when the
    /// jungle has heard enough music, each at its own pace, then sways to the beat and bounces on every beat
    /// (a hop on every beat in the chorus). A new sprout starts a little off the beat and eases into step
    /// with the others over a few seconds (rules in <see cref="SproutSync"/>). Beat comes from the master
    /// clock, so it moves in time with the music, never with the frame rate. Motion starts a few seconds
    /// after load; reduced motion keeps it still.
    /// </summary>
    public abstract class FloorSprout : MonoBehaviour
    {
        const float SwayDegrees = 9f, BeatSquash = 0.14f, ChorusHop = 0.06f;

        public static bool ReducedMotion { get; set; }

        [Tooltip("The look that sways and squashes; the root keeps the hidden/grown size.")]
        [SerializeField] Transform visual;
        [SerializeField] float fullScale = 1f;

        float m_SproutStart = -1f;
        float m_StartDelay, m_GrowSeconds = SproutSync.MinGrowSeconds, m_OffsetBeats;
        Vector3 m_VisualHome;
        bool m_Captured;

        public bool Sprouted => m_SproutStart >= 0f;
        /// <summary>Fully grown (the flower then blooms; the beat offset starts fading).</summary>
        public bool Grown => Sprouted && SecondsSinceGrown >= 0f;
        public bool Chorus { get; set; }
        public Transform Visual => visual;
        public float FullScale => fullScale;
        /// <summary>How far off the beat it sways now, in beats (0 once it has settled into step).</summary>
        public float OffsetBeats => SproutSync.OffsetBeats(m_OffsetBeats, SecondsSinceGrown);
        float SecondsSinceGrown => Time.time - m_SproutStart - m_StartDelay - m_GrowSeconds;

        public void Configure(Transform look, float grownSize)
        {
            visual = look;
            fullScale = grownSize;
        }

        /// <summary>Starts growing, at its own pace and a little off the beat.</summary>
        public void Sprout() => Sprout(Random.value, Random.value, Random.value);

        /// <summary>Sprout with chosen randomness (0..1 each), for tests.</summary>
        public void Sprout(float delay01, float pace01, float offset01)
        {
            if (Sprouted) return;
            m_SproutStart = Time.time;
            m_StartDelay = SproutSync.StartDelay(delay01);
            m_GrowSeconds = SproutSync.GrowSeconds(pace01);
            m_OffsetBeats = SproutSync.InitialOffsetBeats(offset01);
            Debug.Log($"[RoomOrchestra] Jungle: {name} sprouts");
        }

        /// <summary>Back out of sight at once (cleared, under the smoke).</summary>
        public void Unsprout()
        {
            m_SproutStart = -1f;
            Chorus = false;
            transform.localScale = Vector3.one * SproutSync.HiddenScale;
            if (visual != null && m_Captured)
            {
                visual.localPosition = m_VisualHome;
                visual.localRotation = Quaternion.identity;
                visual.localScale = Vector3.one;
            }
            OnUnsprout();
        }

        /// <summary>Called every frame while growing and after (a flower blooms here).</summary>
        protected virtual void OnGrowing(float secondsSinceGrown, bool reducedMotion) { }

        protected virtual void OnUnsprout() { }

        void Update()
        {
            if (Time.timeSinceLevelLoad < JellyBody.StartDelaySeconds || visual == null) return;
            if (!m_Captured)
            {
                m_Captured = true;
                m_VisualHome = visual.localPosition;
            }
            if (!Sprouted) return;

            float grown = ReducedMotion ? 1f : SproutSync.Size(Time.time - m_SproutStart, m_StartDelay, m_GrowSeconds);
            var size = Vector3.one * Mathf.LerpUnclamped(SproutSync.HiddenScale, fullScale, grown);
            if (transform.localScale != size) transform.localScale = size;
            OnGrowing(ReducedMotion ? 10f : SecondsSinceGrown, ReducedMotion);

            if (ReducedMotion) return;
            var conductor = EchoConductor.Instance;
            if (conductor == null || conductor.IsPaused) return;
            // Its own beat: the master clock's, shifted by an offset that fades as it falls into step.
            double bar = conductor.Clock.BarPhase(AudioSettings.dspTime) + OffsetBeats / MusicClock.BeatsPerBar;
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
