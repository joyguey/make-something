using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// The title's Start button for bare hands (owner, Fable refinement 2026-11-11: "if the player chooses to play with
    /// their hands instead of a controller, can you turn the start sign into a handprint as the background with an
    /// outline with a slight green hue"). With controllers it is the green button; with hands it becomes a green
    /// handprint with a pale green outline, "Start" on its palm. Look and pinch still presses it, and so does putting a
    /// hand flat on the print. Either look keeps its own tap area.
    /// </summary>
    public class HandprintStart : MonoBehaviour
    {
        /// <summary>How close to the print a palm counts as placed on it (m).</summary>
        public const float TouchDepth = 0.05f;

        [SerializeField] GameObject buttonLook, handLook;
        [SerializeField] TitleStartButton start;
        [SerializeField] Vector2 printHalfSize = new Vector2(0.06f, 0.085f);

        bool m_Hands;
        bool m_Applied;
        float m_QuietUntil;

        /// <summary>Show the handprint whatever is in use (snapshots); null follows the player's hands.</summary>
        public static bool? Force;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Force = null;

        public bool ShowingHandprint => m_Hands;

        public void Configure(GameObject button, GameObject handprint, TitleStartButton startButton, Vector2 halfSize)
        {
            buttonLook = button;
            handLook = handprint;
            start = startButton;
            printHalfSize = halfSize;
        }

        void OnEnable() => Apply(Force ?? InstrumentInputRouter.UsingHands);

        void Update()
        {
            bool hands = Force ?? InstrumentInputRouter.UsingHands;
            if (hands != m_Hands || !m_Applied) Apply(hands);
            if (!hands || TitleScreen.Instance == null || !TitleScreen.Instance.Showing) return;
            // not while the sign is on its way to the player (a raised hand in its path is not a high five), nor just after
            if (TitleReach.Moving) m_QuietUntil = Time.time + 0.6f;
            if (Time.time < m_QuietUntil) return;
            for (int hand = 0; hand < 2; hand++)
                if (InstrumentInputRouter.TryGetPalm(hand, out var palm) && Touches(palm))
                {
                    start.Press();
                    return;
                }
        }

        /// <summary>Shows the handprint (<paramref name="hands"/>) or the button.</summary>
        public void Apply(bool hands)
        {
            m_Hands = hands;
            m_Applied = true;
            if (buttonLook != null) buttonLook.SetActive(!hands);
            if (handLook != null) handLook.SetActive(hands);
        }

        /// <summary>A palm at <paramref name="palm"/> (scene space) is flat on the print.</summary>
        public bool Touches(Vector3 palm)
        {
            if (handLook == null) return false;
            var local = handLook.transform.InverseTransformPoint(palm);
            float depth = Mathf.Abs(local.z) * Mathf.Max(1e-4f, handLook.transform.lossyScale.z);
            return Mathf.Abs(local.x) <= printHalfSize.x && Mathf.Abs(local.y) <= printHalfSize.y && depth <= TouchDepth;
        }
    }
}
