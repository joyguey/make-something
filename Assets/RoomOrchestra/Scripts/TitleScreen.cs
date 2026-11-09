using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// The title card the game opens on (Version 1 feedback: "boot into a proper title screen"): the name in
    /// the key-art lettering, a line about what it is, and a Start button. While it shows, only Start (a tap,
    /// or the controller's select) does anything. Start sends the card shrinking into the ball stand's cloud,
    /// then the opening plays (<see cref="IntroSequence"/>). It is parked inside the cloud, not shrunk to
    /// nothing (PICO Spatial once drew a 0.001-scaled object full size in the wrong place), and follows the
    /// cloud wherever the ball stand goes.
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        public const float LeaveSeconds = 0.7f;
        /// <summary>The card's scale while parked: small enough to fit inside the cloud's core.</summary>
        public const float ParkedScale = 0.05f;

        [SerializeField] Transform card;
        [Tooltip("Where the card waits once the game has begun (inside the ball stand's cloud).")]
        [SerializeField] Transform park;
        [SerializeField] IntroSequence intro;

        Vector3 m_ShownPosition, m_ShownScale;
        Quaternion m_ShownRotation;
        float m_LeaveStart = -1f;
        bool m_Begun, m_Parked;

        public static TitleScreen Instance { get; private set; }
        /// <summary>True from launch until Start; input other than Start is ignored meanwhile.</summary>
        public bool Showing => !m_Begun;
        public bool Parked => m_Parked;
        public Transform Card => card;

        public void Configure(Transform titleCard, Transform parkPoint, IntroSequence opening)
        {
            card = titleCard;
            park = parkPoint;
            intro = opening;
        }

        void OnEnable() => Instance = this;

        void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Start: the card flies into the cloud and the opening begins.</summary>
        public bool Begin()
        {
            if (m_Begun) return false;
            m_Begun = true;
            Debug.Log("[RoomOrchestra] Title: start");
            return true;
        }

        /// <summary>Parked at once (skipping the opening, and the state the layout tests check).</summary>
        public void ParkNow()
        {
            m_Begun = true;
            Capture();
            if (card != null && park != null)
            {
                card.position = park.position;
                card.localScale = m_ShownScale * ParkedScale;
            }
            Park();
        }

        // Inside the cloud the Start button must not catch taps meant for anything else.
        void Park()
        {
            m_Parked = true;
            if (card != null)
                foreach (var c in card.GetComponentsInChildren<Collider>()) c.enabled = false;
        }

        void Capture()
        {
            if (m_ShownScale != Vector3.zero || card == null) return;
            m_ShownPosition = card.position;
            m_ShownRotation = card.rotation;
            m_ShownScale = card.localScale;
        }

        void Update()
        {
            if (card == null) return;
            // The ball stand travels with the leaf when the player moves (RoamingTray); the parked card goes with
            // it. It was left behind at the stand's old place, a little title card in mid-air (owner, emulator).
            if (m_Parked)
            {
                if (park != null && card.position != park.position) card.position = park.position;
                return;
            }
            if (!m_Begun) return;
            // Nothing moves before PICO Spatial's renderer connects; a very early Start waits for it.
            if (Time.timeSinceLevelLoad < JellyBody.StartDelaySeconds) return;
            if (m_LeaveStart < 0f)
            {
                Capture();
                m_LeaveStart = Time.time;
            }
            float t = Mathf.Clamp01((Time.time - m_LeaveStart) / LeaveSeconds);
            float u = t * t * (3f - 2f * t);
            var target = park != null ? park.position : m_ShownPosition;
            // A little lift first, then down into the cloud, shrinking as it goes.
            var arc = Vector3.up * Mathf.Sin(u * Mathf.PI) * 0.15f;
            card.position = Vector3.Lerp(m_ShownPosition, target, u) + arc;
            card.localScale = m_ShownScale * Mathf.Lerp(1f, ParkedScale, u);
            card.rotation = m_ShownRotation;
            if (t >= 1f)
            {
                Park();
                Debug.Log("[RoomOrchestra] Title: tucked into the cloud; the opening begins");
                if (intro != null) intro.Play();
            }
        }
    }
}
