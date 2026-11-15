using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// With bare hands, the title sign comes to the player (owner, headset 2026-11-14: "the sign needs to be closer for
    /// me to put my hand on it"): it glides from its place 1.8 m away to arm's reach, a little smaller, still standing on
    /// the ground, so a palm can push it and high-five its Start print without a step. With controllers it stays where
    /// it was (their rays reach it), and it goes back if the player picks the controllers up again. Nobody has to reach:
    /// look and pinch presses Start from any distance. Only while the title shows.
    /// </summary>
    public class TitleReach : MonoBehaviour
    {
        /// <summary>From the player's eyes to the sign's face (m): a flat palm reaches it with the arm nearly straight.</summary>
        public const float Distance = 0.6f;
        /// <summary>The sign's size near a seated and a standing player (it keeps its feet on the ground, so a smaller
        /// sign is a lower one), and the eye heights (m) those are for.</summary>
        public const float SeatedScale = 0.8f, StandingScale = 0.95f, SeatedEyes = 1.2f, StandingEyes = 1.55f;
        public const float GlideSeconds = 0.8f;
        /// <summary>Lift at the middle of the glide (m): the sign passes over the ball's cloud, not through it.</summary>
        public const float Arc = 0.45f;
        /// <summary>It comes again if the player ends up further than this from it (m).</summary>
        public const float ComeAgainBeyond = 1.1f;

        [SerializeField] float cardHeight = 1.4f; // the card's middle above the ground at full size (m)

        Vector3 m_HomePosition, m_HomeScale, m_FromPosition, m_FromScale, m_ToPosition, m_ToScale;
        Quaternion m_HomeRotation, m_FromRotation, m_ToRotation;
        bool m_Captured, m_Near;
        float m_Glide = 1f;

        /// <summary>The sign is on its way (its Start print ignores a hand in its path meanwhile).</summary>
        public static bool Moving { get; private set; }
        public bool Near => m_Near;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Moving = false;

        public void Configure(float cardCentreHeight) => cardHeight = cardCentreHeight;

        /// <summary>Where the sign stands for a player whose eyes are at <paramref name="eyes"/>: position of the card's
        /// middle, the way it faces, and its size (1 = full). <paramref name="home"/> is where it stands at launch and
        /// <paramref name="ground"/> the height of the ground.</summary>
        public static (Vector3 position, Quaternion rotation, float scale) NearPose(Vector3 eyes, Vector3 home, float ground, float cardCentreHeight, Vector3 fallbackForward)
        {
            var toward = home - eyes;
            toward.y = 0f;
            if (toward.sqrMagnitude < 0.04f) toward = new Vector3(fallbackForward.x, 0f, fallbackForward.z); // standing at it already
            if (toward.sqrMagnitude < 1e-6f) toward = Vector3.forward;
            toward.Normalize();
            float scale = Mathf.Lerp(SeatedScale, StandingScale, Mathf.InverseLerp(SeatedEyes, StandingEyes, eyes.y - ground));
            var position = eyes + toward * Distance;
            position.y = ground + cardCentreHeight * scale;
            return (position, Quaternion.LookRotation(toward, Vector3.up), scale);
        }

        void LateUpdate()
        {
            var title = TitleScreen.Instance;
            if (title == null || !title.Showing)
            {
                Moving = false;
                enabled = false; // Start was pressed: the title flies into the cloud from wherever the sign is
                return;
            }
            if (!m_Captured)
            {
                m_Captured = true;
                m_HomePosition = transform.localPosition;
                m_HomeRotation = transform.localRotation;
                m_HomeScale = transform.localScale;
            }
            bool hands = InstrumentInputRouter.UsingHands;
            if (hands && StageAnchor.Instance != null && StageAnchor.Instance.TryGetHeadPose(out var head))
            {
                var flat = transform.position - head.position;
                flat.y = 0f;
                if (!m_Near || (m_Glide >= 1f && flat.magnitude > ComeAgainBeyond)) Come(head);
            }
            else if (!hands && m_Near) GoHome();

            if (m_Glide >= 1f)
            {
                Moving = false;
                return;
            }
            Moving = true;
            m_Glide = Mathf.Min(1f, m_Glide + Time.deltaTime / GlideSeconds);
            float e = m_Glide * m_Glide * (3f - 2f * m_Glide);
            transform.localPosition = Vector3.Lerp(m_FromPosition, m_ToPosition, e) + Vector3.up * (Mathf.Sin(e * Mathf.PI) * Arc);
            transform.localRotation = Quaternion.Slerp(m_FromRotation, m_ToRotation, e);
            transform.localScale = Vector3.Lerp(m_FromScale, m_ToScale, e);
        }

        void Come(Pose head)
        {
            var parent = transform.parent;
            Vector3 World(Vector3 local) => parent != null ? parent.TransformPoint(local) : local;
            var home = World(m_HomePosition);
            var (position, rotation, scale) = NearPose(head.position, home, home.y - cardHeight, cardHeight, head.rotation * Vector3.forward);
            Glide(parent != null ? parent.InverseTransformPoint(position) : position,
                parent != null ? Quaternion.Inverse(parent.rotation) * rotation : rotation, m_HomeScale * scale);
            m_Near = true;
            Debug.Log($"[RoomOrchestra] Title: hands in use, the sign comes within reach (size {scale:F2})");
        }

        void GoHome()
        {
            Glide(m_HomePosition, m_HomeRotation, m_HomeScale);
            m_Near = false;
            Debug.Log("[RoomOrchestra] Title: controllers in use, the sign goes back");
        }

        void Glide(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            m_FromPosition = transform.localPosition;
            m_FromRotation = transform.localRotation;
            m_FromScale = transform.localScale;
            m_ToPosition = position;
            m_ToRotation = rotation;
            m_ToScale = scale;
            m_Glide = 0f;
        }
    }
}
