using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Plays <see cref="PressJiggleCurve"/> on the object that took a press: a wobble about the line from the player's
    /// eyes (so it reads the same from any side) and a small swell, then puts the transform back exactly as it was.
    /// Added on the first press (<see cref="Play"/>), never at startup, and only touches the transform while jiggling
    /// (PICO Spatial: change nothing visible at startup). With reduced motion it flashes brighter instead.
    /// </summary>
    public sealed class PressJiggle : MonoBehaviour
    {
        /// <summary>Reduced motion: a brief brightening instead of the wobble (accessibility, motion sensitivity).</summary>
        public static bool ReducedMotion { get; set; }

        Quaternion m_BaseRotation;
        Vector3 m_BaseScale;
        bool m_Swell;
        float m_Start = -1f;

        public bool Jiggling => m_Start >= 0f;

        /// <summary>Jiggles <paramref name="target"/> (or flashes it, with reduced motion); false if there's nothing to do.</summary>
        public static bool Play(Component target, bool swell = true)
        {
            if (target == null) return false;
            if (ReducedMotion)
            {
                var flash = target.GetComponent<ColorFlash>();
                if (flash == null) flash = target.gameObject.AddComponent<ColorFlash>();
                flash.Flash();
                return true;
            }
            // a toy button presses down instead of wobbling (owner, 2026-11-19)
            if (target.TryGetComponent<ToyPress>(out var toy) && toy.Cap != null)
            {
                toy.Press();
                return true;
            }
            var jiggle = target.GetComponent<PressJiggle>();
            if (jiggle == null) jiggle = target.gameObject.AddComponent<PressJiggle>();
            jiggle.Begin(swell);
            return true;
        }

        void Begin(bool swell)
        {
            if (!Jiggling)
            {
                m_BaseRotation = transform.localRotation;
                m_BaseScale = transform.localScale;
            }
            m_Swell = swell;
            m_Start = Time.unscaledTime;
            enabled = true;
        }

        void Update()
        {
            if (!Jiggling)
            {
                enabled = false;
                return;
            }
            float t = Time.unscaledTime - m_Start;
            transform.localRotation = m_BaseRotation;
            if (t >= PressJiggleCurve.Duration)
            {
                if (m_Swell) transform.localScale = m_BaseScale;
                m_Start = -1f;
                enabled = false;
                return;
            }
            var eye = Camera.main != null ? Camera.main.transform.position : transform.position - Vector3.forward;
            var axis = transform.position - eye;
            if (axis.sqrMagnitude < 1e-6f) axis = Vector3.forward;
            transform.rotation = Quaternion.AngleAxis(PressJiggleCurve.Angle(t), axis.normalized) * transform.rotation;
            if (m_Swell) transform.localScale = m_BaseScale * (1f + PressJiggleCurve.Swell(t));
        }

        void OnDisable()
        {
            if (!Jiggling) return;
            transform.localRotation = m_BaseRotation;
            if (m_Swell) transform.localScale = m_BaseScale;
            m_Start = -1f;
        }
    }
}
