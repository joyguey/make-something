using System.Collections.Generic;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// A toy button's press (owner, 2026-11-19: the Astro Bot direction, "put it all into the game"): its soft cap and the
    /// icon on it sink into the cream ring and spring back up, instead of the whole button wobbling (<see cref="PressJiggle"/>).
    /// It moves things by the change in its offset each frame, never by setting a position, so a button that moves its own
    /// icons at the same time (Pause swaps bars and triangle) keeps every position it set. Only touches transforms while
    /// pressing; nothing at startup.
    /// </summary>
    public sealed class ToyPress : MonoBehaviour
    {
        /// <summary>How far the cap sinks (m) and how long the press lasts (s).</summary>
        public const float Depth = 0.008f, Seconds = 0.22f;

        [SerializeField] Transform cap;
        [Tooltip("What rides on the cap: the icon parts (children whose names contain \"Icon\" are found at the press).")]
        [SerializeField] List<Transform> riders = new();

        float m_Start = -1f;
        float m_Offset;

        public bool Pressing => m_Start >= 0f;
        public Transform Cap => cap;
        /// <summary>The cap's current depth below its rest (m), for tests.</summary>
        public float Offset => m_Offset;

        public void Configure(Transform capPart)
        {
            cap = capPart;
            enabled = false;
        }

        /// <summary>Starts (or restarts) the press.</summary>
        public void Press()
        {
            if (cap == null) return;
            if (!Pressing)
            {
                riders.Clear();
                riders.Add(cap);
                foreach (Transform child in transform)
                    if (child != cap && child.name.Contains("Icon")) riders.Add(child);
            }
            m_Start = Time.unscaledTime;
            enabled = true;
        }

        /// <summary>Down fast (a quarter of the time), back up with one small overshoot, then exactly home.</summary>
        public static float Curve(float t)
        {
            if (t <= 0f || t >= Seconds) return 0f;
            float down = Seconds * 0.25f;
            if (t < down) return Mathf.SmoothStep(0f, 1f, t / down);
            float u = (t - down) / (Seconds - down);
            return Mathf.Cos(u * Mathf.PI * 0.5f) * (1f - u) - 0.12f * Mathf.Sin(u * Mathf.PI) * u;
        }

        void Update()
        {
            if (!Pressing)
            {
                enabled = false;
                return;
            }
            float t = Time.unscaledTime - m_Start;
            float target = t >= Seconds ? 0f : Curve(t) * Depth;
            Move(target - m_Offset);
            m_Offset = target;
            if (t >= Seconds)
            {
                m_Start = -1f;
                enabled = false;
            }
        }

        void Move(float down)
        {
            if (Mathf.Approximately(down, 0f)) return;
            foreach (var r in riders)
                if (r != null) r.localPosition += Vector3.down * down;
        }

        void OnDisable()
        {
            // never left half pressed (hidden mid-press, or the scene ends)
            Move(-m_Offset);
            m_Offset = 0f;
            m_Start = -1f;
        }
    }
}
