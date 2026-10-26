using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// A little cloud of puffs that bursts where a character lands on the floor. The puffs are authored
    /// in the scene and wait inside the ball stand's post (PICO Spatial: no runtime objects, and inactive
    /// objects still draw, so hidden means parked out of sight). A few clouds share the work.
    /// </summary>
    public class PoofCloud : MonoBehaviour
    {
        public const float Seconds = 0.6f;
        const float Spread = 0.12f, Rise = 0.06f, Grow = 3.2f;

        static readonly System.Collections.Generic.List<PoofCloud> s_All = new();

        [SerializeField] Transform[] puffs;
        [Tooltip("Where the cloud waits, wholly inside the ball stand.")]
        [SerializeField] Transform parkPoint;

        Vector3[] m_Directions;
        float[] m_Sizes;
        Vector3 m_PuffScale;
        float m_Start = -10f;

        public bool Playing => Time.time - m_Start < Seconds;
        public System.Collections.Generic.IReadOnlyList<Transform> Puffs => puffs;

        public void Configure(Transform[] cloudPuffs, Transform park)
        {
            puffs = cloudPuffs;
            parkPoint = park;
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
        }

        void Update()
        {
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
