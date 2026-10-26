using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// A character's face: idle blinks, a surprised "oh" with wide eyes, a sideways glance at a
    /// sibling, and a small squash-and-stretch bounce. Face parts only move (the "oh" mouth waits
    /// inside the body), so PICO Spatial never has to show, hide or create anything. Transforms are
    /// written only when they change. Reduced motion keeps the face changes but drops blinks and
    /// bounces (no player-facing switch yet: the owner removed the Calm button).
    /// </summary>
    public class CharacterFace : MonoBehaviour
    {
        public const float HoldSeconds = 1.2f;
        const float BlinkSeconds = 0.12f, BounceSeconds = 0.3f;
        const float GlanceOffset = 0.05f, WideEyes = 1.3f;
        static readonly Vector3 Parked = new Vector3(0f, 0f, 0.25f); // sinks a mouth wholly into the body

        public static bool ReducedMotion { get; set; }

        [Tooltip("Squashes for the bounce; leave empty when something else owns this scale (tray tokens).")]
        [SerializeField] Transform visual;
        [SerializeField] Transform eyeL, eyeR, smile;
        [Tooltip("Optional surprised mouth; without it surprise is shown by the eyes alone.")]
        [SerializeField] Transform mouthO;

        Vector3 m_EyeLHome, m_EyeRHome, m_EyeScale, m_SmileHome, m_MouthParked, m_VisualScale;
        bool m_Captured;
        float m_Until, m_Glance, m_NextBlink, m_BounceStart = -10f;

        public Expression Current { get; private set; }
        public Transform EyeL => eyeL;
        public Transform Smile => smile;
        public Transform MouthO => mouthO;
        public Transform Visual => visual;

        public void Configure(Transform squash, Transform leftEye, Transform rightEye, Transform smileMouth, Transform ohMouth = null)
        {
            visual = squash;
            eyeL = leftEye;
            eyeR = rightEye;
            smile = smileMouth;
            mouthO = ohMouth;
        }

        // Authored pose = idle pose; read lazily so nothing changes at startup.
        void Capture()
        {
            if (m_Captured) return;
            m_Captured = true;
            m_EyeLHome = eyeL.localPosition;
            m_EyeRHome = eyeR.localPosition;
            m_EyeScale = eyeL.localScale;
            m_SmileHome = smile.localPosition;
            if (mouthO != null) m_MouthParked = mouthO.localPosition;
            if (visual != null) m_VisualScale = visual.localScale;
            m_NextBlink = Time.time + 2f + Random.value * 4f;
        }

        /// <summary>Pulls <paramref name="expression"/> for a moment. <paramref name="glance"/>: -1 left, +1 right (player's view).</summary>
        public void Show(Expression expression, float glance = 0f)
        {
            Capture();
            Current = expression;
            m_Glance = Mathf.Clamp(glance, -1f, 1f);
            m_Until = Time.time + HoldSeconds;
            Apply();
        }

        public void Bounce()
        {
            Capture();
            if (ReducedMotion || visual == null) return;
            m_BounceStart = Time.time;
        }

        void Update()
        {
            if (!m_Captured) return; // untouched until the first reaction
            if (Current != Expression.Idle && Time.time >= m_Until) Current = Expression.Idle;
            Apply();
        }

        void Apply()
        {
            float now = Time.time;
            bool blinking = false;
            if (!ReducedMotion && Current == Expression.Idle)
            {
                if (now >= m_NextBlink + BlinkSeconds) m_NextBlink = now + 3f + Random.value * 3f;
                blinking = now >= m_NextBlink;
            }

            var eyeScale = m_EyeScale;
            if (Current == Expression.Curious) eyeScale *= WideEyes;
            if (blinking) eyeScale.y *= 0.2f;
            var shift = Current == Expression.Sibling ? Vector3.right * (m_Glance * GlanceOffset) : Vector3.zero;
            Set(eyeL, m_EyeLHome + shift, eyeScale);
            Set(eyeR, m_EyeRHome + shift, eyeScale);

            bool oh = Current == Expression.Curious && mouthO != null;
            SetPosition(smile, oh ? m_SmileHome + Parked : m_SmileHome);
            if (mouthO != null) SetPosition(mouthO, oh ? m_MouthParked - Parked : m_MouthParked);

            if (visual != null)
            {
                float t = (now - m_BounceStart) / BounceSeconds;
                float squash = t >= 0f && t < 1f ? Mathf.Sin(t * Mathf.PI) * 0.12f : 0f;
                var scale = Vector3.Scale(m_VisualScale, new Vector3(1f + squash, 1f - squash, 1f + squash));
                if (visual.localScale != scale) visual.localScale = scale;
            }
        }

        static void Set(Transform t, Vector3 position, Vector3 scale)
        {
            SetPosition(t, position);
            if (t.localScale != scale) t.localScale = scale;
        }

        static void SetPosition(Transform t, Vector3 position)
        {
            if (t != null && t.localPosition != position) t.localPosition = position;
        }
    }
}
