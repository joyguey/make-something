using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>The three ways in from the title card (owner, 2026-11-26: "Story", "Sandbox" and "Play together",
    /// layout A with a handprint for each).</summary>
    public enum TitleChoice { Sandbox, Story, Together }

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
        float m_LeaveStart = -1f, m_ReturnStart = -1f;
        /// <summary>How long the card takes to grow back in place when it returns (s).</summary>
        public const float ReturnSeconds = 0.35f;
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

        /// <summary>What the player chose on the card.</summary>
        public TitleChoice Choice { get; private set; }

        /// <summary>A choice on the card: the card flies into the cloud and the opening begins; then Story opens, or the
        /// Play together card, or (Sandbox) the opening and free play as before.</summary>
        public bool Begin() => Begin(TitleChoice.Sandbox); // also reached by name (SendMessage), which cannot pass a choice

        public bool Begin(TitleChoice choice)
        {
            if (m_Begun) return false;
            m_Begun = true;
            Choice = choice;
            Debug.Log($"[RoomOrchestra] Title: start ({choice})");
            return true;
        }

        /// <summary>After the card has tucked away: Story begins (a first Story skips nothing: the sandbox's lesson waits
        /// until the player first plays the sandbox, owner 2026-11-26), or the Play together card comes out.</summary>
        void RunChoice()
        {
            switch (Choice)
            {
                case TitleChoice.Story:
                    StoryDirector.Instance?.BeginFromTitle();
                    break;
                case TitleChoice.Together:
                    MenuTray.Instance?.SetOpen(false);
                    var lesson = TutorialDirector.Instance;
                    if (lesson != null && lesson.Active) lesson.Skip();
                    EchoBattleDirector.Instance?.ShowFromMenu();
                    break;
            }
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

        /// <summary>
        /// Back to the card (owner, headset 2026-11-26: "When I leave the story mode game, shouldn't it take me back to
        /// the sign post at the beginning so I can select sandbox?"): after Story chosen here, Leave brings the card back
        /// where it first stood. It grows back in place; it never glides toward the player. Its three choices work again.
        /// </summary>
        public bool ShowAgain()
        {
            if (card == null || !m_Parked || m_ShownScale == Vector3.zero) return false;
            m_Begun = false;
            m_Parked = false;
            m_LeaveStart = -1f;
            m_ReturnStart = Time.time;
            card.position = m_ShownPosition;
            card.rotation = m_ShownRotation;
            card.localScale = m_ShownScale * 0.7f;
            foreach (var c in card.GetComponentsInChildren<Collider>()) c.enabled = true;
            Debug.Log("[RoomOrchestra] Title: the card is back");
            return true;
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
            if (!m_Begun)
            {
                if (m_ReturnStart >= 0f)
                {
                    float r = Mathf.Clamp01((Time.time - m_ReturnStart) / ReturnSeconds);
                    card.localScale = m_ShownScale * Mathf.Lerp(0.7f, 1f, r * r * (3f - 2f * r));
                    if (r >= 1f) m_ReturnStart = -1f;
                }
                return;
            }
            m_ReturnStart = -1f;
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
                // Story chosen here: the sandbox's opening waits (headset 2026-11-26: it played hidden behind Story,
                // the room scan and "where should the Handpan live?" with it); it plays once Sandbox is chosen
                if (Choice == TitleChoice.Story) Debug.Log("[RoomOrchestra] Title: tucked into the cloud; Story first, the opening waits");
                else
                {
                    Debug.Log("[RoomOrchestra] Title: tucked into the cloud; the opening begins");
                    if (intro != null) intro.Play();
                }
                RunChoice();
            }
        }
    }
}
