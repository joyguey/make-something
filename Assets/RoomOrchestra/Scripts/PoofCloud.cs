using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// A little cloud of puffs that bursts where a character lands on the floor. The puffs are authored
    /// in the scene and wait inside the ball stand's cloud (PICO Spatial: no runtime objects, and inactive
    /// objects still draw, so hidden means parked out of sight). A few clouds share the work.
    /// A character landing is silent (owner 2026-10-28); Clear's big clouds play the smoky whoosh
    /// (Audio/Effects, ArtSource/Audio/gen_poof.py). Clouds that burst together share one sound so the
    /// smoke never gets loud.
    /// </summary>
    public class PoofCloud : MonoBehaviour
    {
        public const float Seconds = 0.6f;
        const float Spread = 0.12f, Rise = 0.06f, Grow = 3.2f;
        /// <summary>Clouds at least this big (Clear's smoke) play the whoosh; smaller ones (landings) are silent.</summary>
        public const float SmokeSize = 2f;
        /// <summary>Bursts closer together than this share one sound.</summary>
        public const float SoundGapSeconds = 0.15f;
        // Small pitch steps in turn, so repeated poofs are not identical.
        static readonly float[] s_Pitches = { 1f, 1.07f, 0.94f };
        static float s_LastSound = -10f;
        static int s_Variant;

        static readonly System.Collections.Generic.List<PoofCloud> s_All = new();

        [SerializeField] Transform[] puffs;
        [Tooltip("Where the cloud waits, wholly inside the ball stand.")]
        [SerializeField] Transform parkPoint;
        [SerializeField] AudioSource voice;
        [SerializeField] AudioClip smokeSound;

        Vector3[] m_Directions;
        float[] m_Sizes;
        Vector3 m_PuffScale;
        float m_Start = -10f;

        public bool Playing => Time.time - m_Start < Seconds;
        public System.Collections.Generic.IReadOnlyList<Transform> Puffs => puffs;

        /// <summary>The sound the last burst started, or null if it was silent or shared another cloud's.</summary>
        public AudioClip LastSound { get; private set; }

        /// <summary>Every idle cloud back into the stand at once (the stand moved while nothing updates, e.g. a skip).</summary>
        public static void ParkIdle()
        {
            foreach (var cloud in FindObjectsByType<PoofCloud>(FindObjectsSortMode.None))
                if (cloud.m_Start < 0f && cloud.parkPoint != null) cloud.transform.position = cloud.parkPoint.position;
        }

        public void Configure(Transform[] cloudPuffs, Transform park)
        {
            puffs = cloudPuffs;
            parkPoint = park;
        }

        public void ConfigureSound(AudioSource source, AudioClip smoke)
        {
            voice = source;
            smokeSound = smoke;
        }

        // Fresh each play session, even with domain reload off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSound()
        {
            s_LastSound = -10f;
            s_Variant = 0;
        }

        void OnEnable() => s_All.Add(this);
        void OnDisable() => s_All.Remove(this);

        /// <summary>
        /// Plays a free cloud at <paramref name="position"/>, <paramref name="size"/> times the landing poof
        /// (Clear uses big ones); false if every cloud is busy.
        /// </summary>
        public static bool PoofAt(Vector3 position, float size = 1f)
        {
            foreach (var cloud in s_All)
                if (!cloud.Playing)
                {
                    cloud.Play(position, size);
                    return true;
                }
            return false;
        }

        public void Play(Vector3 position, float size = 1f)
        {
            transform.localScale = Vector3.one * size;
            if (m_Directions == null)
            {
                m_PuffScale = puffs[0].localScale;
                // Puff 0 is the big middle one that billows up; the rest ring it, big and small in turn,
                // overlapping so they read as one cartoon cloud rather than separate balls.
                m_Directions = new Vector3[puffs.Length];
                m_Sizes = new float[puffs.Length];
                m_Directions[0] = new Vector3(0f, 1.2f, 0f);
                m_Sizes[0] = 1.5f;
                for (int i = 1; i < puffs.Length; i++)
                {
                    float a = i * Mathf.PI * 2f / (puffs.Length - 1);
                    m_Directions[i] = new Vector3(Mathf.Cos(a) * 0.75f, 0.3f + 0.4f * (i % 2), Mathf.Sin(a) * 0.75f);
                    m_Sizes[i] = i % 2 == 0 ? 1.1f : 0.8f;
                }
            }
            transform.position = position;
            m_Start = Time.time;
            Animate(0f);
            PlaySound(size);
        }

        void PlaySound(float size)
        {
            LastSound = null;
            if (size < SmokeSize) return; // a landing poof is silent
            var clip = smokeSound;
            if (voice == null || clip == null || Time.time - s_LastSound < SoundGapSeconds) return;
            s_LastSound = Time.time;
            voice.pitch = s_Pitches[s_Variant++ % s_Pitches.Length];
            voice.clip = clip;
            voice.Play();
            LastSound = clip;
        }

        void Update()
        {
            // Waiting: stay inside the ball stand, wherever the leaf pad has carried it.
            if (m_Start < 0f && parkPoint != null && transform.position != parkPoint.position) transform.position = parkPoint.position;
            if (m_Directions == null || m_Start < 0f) return;
            float t = (Time.time - m_Start) / Seconds;
            if (t < 1f)
            {
                Animate(t);
                return;
            }
            // Done: back inside the stand, puffs gathered at its centre.
            m_Start = -10f;
            for (int i = 0; i < puffs.Length; i++)
            {
                puffs[i].localPosition = Vector3.zero;
                puffs[i].localScale = m_PuffScale;
            }
            transform.localScale = Vector3.one; // back to the size that fits inside the stand
            if (parkPoint != null) transform.position = parkPoint.position;
        }

        // Puffs burst outward and up, swell, then shrink away.
        void Animate(float t)
        {
            float ease = 1f - (1f - t) * (1f - t);
            float size = Mathf.Lerp(1f, Grow, Mathf.Min(1f, t * 3f)) * (1f - t * t);
            for (int i = 0; i < puffs.Length; i++)
            {
                var d = m_Directions[i];
                puffs[i].localPosition = new Vector3(d.x * Spread, d.y * Rise / 0.6f, d.z * Spread) * ease;
                puffs[i].localScale = m_PuffScale * Mathf.Max(size * m_Sizes[i], 0.2f);
            }
        }
    }
}
