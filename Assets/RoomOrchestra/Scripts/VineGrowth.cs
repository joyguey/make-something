using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// A vine that creeps up its furniture as music plays: pieces unfurl one by one from inside the leg
    /// or pole (each piece's pivot is inside the furniture, so at the hidden size it is wholly out of
    /// sight; PICO Spatial draws inactive objects, so nothing is switched off). When the last piece is
    /// out, the flowers bloom. Starts hidden: nothing changes at startup.
    /// </summary>
    public class VineGrowth : MonoBehaviour
    {
        /// <summary>Size of a piece still folded inside the furniture.</summary>
        public const float HiddenScale = 0.15f;
        public const float GrowSeconds = 0.9f, StaggerSeconds = 0.2f;

        public static bool ReducedMotion { get; set; }

        [SerializeField] Transform[] segments;
        [SerializeField] Transform[] flowers;

        float[] m_GrowStart;
        int m_Shown;
        float m_BloomStart = -10f;
        bool m_Bloomed;

        public int Shown => m_Shown;
        public bool Bloomed => m_Bloomed;
        public int SegmentCount => segments != null ? segments.Length : 0;
        public System.Collections.Generic.IReadOnlyList<Transform> Segments => segments;
        public System.Collections.Generic.IReadOnlyList<Transform> Flowers => flowers;

        public void Configure(Transform[] pieces, Transform[] blooms)
        {
            segments = pieces;
            flowers = blooms;
        }

        /// <summary>Grows to <paramref name="progress"/> (0..1) of its length; returns true if a new piece or bloom appeared.</summary>
        public bool SetProgress(float progress)
        {
            if (segments == null) return false;
            m_GrowStart ??= new float[segments.Length];
            int target = Mathf.Clamp(Mathf.CeilToInt(progress * segments.Length - 1e-4f), 0, segments.Length);
            bool changed = false;
            int firstNew = m_Shown;
            for (; m_Shown < target; m_Shown++)
            {
                m_GrowStart[m_Shown] = Time.time + StaggerSeconds * (m_Shown - firstNew); // several at once creep up in turn
                changed = true;
            }
            if (!m_Bloomed && m_Shown == segments.Length)
            {
                m_Bloomed = true;
                m_BloomStart = m_GrowStart[segments.Length - 1] + GrowSeconds * 0.7f; // as the tip finishes unfurling
                changed = true;
            }
            return changed;
        }

        void Update()
        {
            if (segments == null || m_GrowStart == null) return;
            for (int i = 0; i < m_Shown; i++) Grow(segments[i], m_GrowStart[i]);
            if (m_Bloomed && flowers != null)
                foreach (var f in flowers) Grow(f, m_BloomStart);
        }

        // Unfurls from the hidden size with a springy overshoot (a cartoon "pop"); reduced motion just appears.
        static void Grow(Transform t, float start)
        {
            if (t == null) return;
            float u = ReducedMotion ? 1f : Mathf.Clamp01((Time.time - start) / GrowSeconds);
            float s = u <= 0f ? HiddenScale : Mathf.Lerp(HiddenScale, 1f, EaseOutBack(u));
            var scale = Vector3.one * s;
            if (t.localScale != scale) t.localScale = scale;
        }

        static float EaseOutBack(float u)
        {
            const float c = 1.70158f; // ~10% overshoot
            float x = u - 1f;
            return 1f + (c + 1f) * x * x * x + c * x * x;
        }
    }
}
