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
        /// <summary>How close to the print a palm counts as placed on it (m), and how much wider than the drawn print the
        /// touch area is (a slap is not aimed to the centimetre).</summary>
        public const float TouchDepth = 0.05f, Generous = 1.3f;

        [SerializeField] GameObject buttonLook, handLook;
        [SerializeField] TitleStartButton start;
        [SerializeField] Vector2 printHalfSize = new Vector2(0.06f, 0.085f);

        bool m_Hands;
        bool m_Applied;

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
            for (int hand = 0; hand < 2; hand++)
            {
                if (!InstrumentInputRouter.TryGetPalm(hand, out var palm))
                {
                    m_HadPalm[hand] = false;
                    continue;
                }
                // on the print, or through it since the last frame (a slap is faster than the frame rate)
                bool touched = Touches(palm) || (m_HadPalm[hand] && Crossed(m_LastPalm[hand], palm));
                m_LastPalm[hand] = palm;
                m_HadPalm[hand] = true;
                if (!touched) continue;
                Debug.Log("[RoomOrchestra] Title: high five on the Start print");
                start.Press();
                return;
            }
        }

        readonly Vector3[] m_LastPalm = new Vector3[2];
        readonly bool[] m_HadPalm = new bool[2];

        /// <summary>A palm that was in front of the print at <paramref name="before"/> and is behind it at
        /// <paramref name="now"/> went through it.</summary>
        public bool Crossed(Vector3 before, Vector3 now)
        {
            if (handLook == null) return false;
            var a = handLook.transform.InverseTransformPoint(before);
            var b = handLook.transform.InverseTransformPoint(now);
            if (a.z >= 0f || b.z < 0f) return false; // the player's side is -Z
            var at = Vector3.Lerp(a, b, a.z / (a.z - b.z)); // where it met the print's plane
            return Mathf.Abs(at.x) <= printHalfSize.x * Generous && Mathf.Abs(at.y) <= printHalfSize.y * Generous;
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
            return Mathf.Abs(local.x) <= printHalfSize.x * Generous && Mathf.Abs(local.y) <= printHalfSize.y * Generous && depth <= TouchDepth;
        }
    }
}
