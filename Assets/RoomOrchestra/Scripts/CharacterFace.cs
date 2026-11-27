using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// A character's face: it blinks every few seconds, opens wide eyes and an "oh" mouth when
    /// surprised, glances sideways at a sibling, squints happily when its own note plays, and looks
    /// down at the drop, wide-eyed, when it is falling to the floor. Face
    /// parts only move (the "oh" mouth waits inside the body), so PICO Spatial never has to show,
    /// hide or create anything. Like JellyBody it starts a few seconds after load (startup pose race).
    /// Reduced motion keeps the expressions but drops blinks.
    /// </summary>
    public class CharacterFace : MonoBehaviour
    {
        public const float HoldSeconds = 1.6f;
        const float BlinkSeconds = 0.13f, HappySeconds = 0.25f;
        const float GlanceOffset = 0.07f, WideEyes = 1.45f, LookDown = 0.05f;
        /// <summary>Frozen in terror: eyes this much wider, the "oh" this size (owner, 2026-11-26: "frozen in terror with a
        /// minimal scared look").</summary>
        const float FrozenEyes = 1.25f, FrozenMouth = 0.6f;
        static readonly Vector3 Parked = new Vector3(0f, 0f, 0.25f); // sinks a mouth wholly into the body

        public static bool ReducedMotion { get; set; }

        [SerializeField] Transform eyeL, eyeR, smile;
        [Tooltip("Optional surprised mouth; without it surprise is shown by the eyes alone.")]
        [SerializeField] Transform mouthO;

        Vector3 m_EyeLHome, m_EyeRHome, m_EyeScale, m_SmileHome, m_MouthParked, m_MouthScale;
        Vector3 m_EyeLCentre, m_EyeRCentre; // eye meshes are built around the body centre, not their own
        bool m_Captured;
        float m_Until, m_Glance, m_NextBlink, m_HappyStart = -10f;

        public Expression Current { get; private set; }
        public Transform EyeL => eyeL;
        public Transform Smile => smile;
        public Transform MouthO => mouthO;

        public void Configure(Transform leftEye, Transform rightEye, Transform smileMouth, Transform ohMouth = null)
        {
            eyeL = leftEye;
            eyeR = rightEye;
            smile = smileMouth;
            mouthO = ohMouth;
        }

        // Authored pose = idle pose, read on first use.
        void Capture()
        {
            if (m_Captured) return;
            m_Captured = true;
            m_EyeLHome = eyeL.localPosition;
            m_EyeRHome = eyeR.localPosition;
            m_EyeScale = eyeL.localScale;
            m_EyeLCentre = MeshCentre(eyeL);
            m_EyeRCentre = MeshCentre(eyeR);
            m_SmileHome = smile.localPosition;
            if (mouthO != null) { m_MouthParked = mouthO.localPosition; m_MouthScale = mouthO.localScale; }
            m_NextBlink = Time.time + 1f + Random.value * 3f;
        }

        /// <summary>Pulls <paramref name="expression"/> for a moment. <paramref name="glance"/>: -1 left, +1 right (player's view).</summary>
        public void Show(Expression expression, float glance = 0f, float hold = HoldSeconds)
        {
            Capture();
            Current = expression;
            m_Glance = Mathf.Clamp(glance, -1f, 1f);
            m_Until = Time.time + hold;
            Apply();
        }

        /// <summary>A short happy squint (its note just played).</summary>
        public void Beam()
        {
            Capture();
            m_HappyStart = Time.time;
        }

        void Update()
        {
            if (!m_Captured && Time.timeSinceLevelLoad < JellyBody.StartDelaySeconds) return;
            if (TryGetComponent<JellyBody>(out var body) && !body.IsAwake && Current == Expression.Idle) return; // parked out of sight
            Capture();
            if (Current != Expression.Idle && Time.time >= m_Until) Current = Expression.Idle;
            Apply();
        }

        void Apply()
        {
            float now = Time.time;
            bool blinking = false;
            if (!ReducedMotion && Current == Expression.Idle)
            {
                if (now >= m_NextBlink + BlinkSeconds) m_NextBlink = now + 2.5f + Random.value * 3f;
                blinking = now >= m_NextBlink;
            }
            bool happy = now - m_HappyStart < HappySeconds;

            // Eyes grow and close flat against the face (x, y), never in depth, so they never bulge out.
            var grow = Vector3.one;
            bool scared = Current == Expression.Scared;
            bool frozen = Current == Expression.Frozen;
            if (Current == Expression.Curious || scared) grow = new Vector3(WideEyes, WideEyes, 1f);
            else if (frozen) grow = new Vector3(FrozenEyes, FrozenEyes, 1f); // a little wide, staring (no blinks: not idle)
            if (blinking) grow.y *= 0.15f;
            else if (happy && Current != Expression.Curious) grow.y *= 0.45f; // smiling eyes
            var shift = Current == Expression.Sibling ? Vector3.right * (m_Glance * GlanceOffset)
                : scared ? Vector3.down * LookDown // eyes on the drop below
                : Vector3.zero;
            Set(eyeL, m_EyeLHome + shift + AboutCentre(m_EyeLCentre, grow), Vector3.Scale(m_EyeScale, grow));
            Set(eyeR, m_EyeRHome + shift + AboutCentre(m_EyeRCentre, grow), Vector3.Scale(m_EyeScale, grow));

            bool oh = (Current == Expression.Curious || scared || frozen) && mouthO != null;
            SetPosition(smile, oh ? m_SmileHome + Parked : m_SmileHome);
            if (mouthO != null)
            {
                SetPosition(mouthO, oh ? m_MouthParked - Parked : m_MouthParked);
                var mouthScale = frozen ? m_MouthScale * FrozenMouth : m_MouthScale; // a small "oh": minimal, not a scream
                if (mouthO.localScale != mouthScale) mouthO.localScale = mouthScale;
            }
        }

        // Offset that keeps a part's own centre in place while its transform scales by grow.
        Vector3 AboutCentre(Vector3 centre, Vector3 grow)
        {
            var c = Vector3.Scale(m_EyeScale, centre);
            return c - Vector3.Scale(grow, c);
        }

        /// <summary>Which way the face looks, flat, in <paramref name="root"/>'s own space (from the eyes' middle).</summary>
        public Vector3 FacingIn(Transform root)
        {
            if (eyeL == null || eyeR == null || root == null) return Vector3.back;
            var eyes = (eyeL.TransformPoint(MeshCentre(eyeL)) + eyeR.TransformPoint(MeshCentre(eyeR))) / 2f;
            var local = root.InverseTransformPoint(eyes);
            local.y = 0f;
            return local.sqrMagnitude > 1e-8f ? local.normalized : Vector3.back;
        }

        static Vector3 MeshCentre(Transform part) =>
            part.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null ? filter.sharedMesh.bounds.center : Vector3.zero;

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
