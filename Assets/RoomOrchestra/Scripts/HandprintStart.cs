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
        /// <summary>A palm is on the print from this far in front of it (m: the tracked palm sits about 2 cm inside the
        /// hand, so this is skin on the sign) to this far behind it (a hand that went through).</summary>
        public const float ContactInFront = 0.03f, ContactBehind = 0.06f;
        /// <summary>How much wider than the drawn print the touch area is (a slap is not aimed to the centimetre).</summary>
        public const float Generous = 1.15f;
        /// <summary>A palm that moved further than this in one frame was not a slap but a tracking jump (m).</summary>
        public const float JumpBeyond = 0.3f;

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
                bool had = m_HadPalm[hand];
                var before = m_LastPalm[hand];
                m_LastPalm[hand] = palm;
                m_HadPalm[hand] = true;
                // a palm that jumped is tracking noise, not a hand (owner, headset 2026-11-15: "it triggered the start
                // without me putting my hand on the sign"); it has to be seen two frames running, close together
                if (!had || Vector3.Distance(before, palm) > JumpBeyond) continue;
                // on the print, or through it since the last frame (a slap is faster than the frame rate)
                if (!Touches(palm) && !Crossed(before, palm))
                {
                    NearMiss(palm);
                    continue;
                }
                Debug.Log($"[RoomOrchestra] Title: high five on the Start print ({DepthOf(palm) * 100f:F1} cm from its face)");
                start.Press();
                return;
            }
        }

        float m_NextMissLog;

        // a palm close to the print that did not count, for the log (owner, headset 2026-11-26: slaps on Story and
        // Sandbox did not register)
        void NearMiss(Vector3 palm)
        {
            if (handLook == null || Time.unscaledTime < m_NextMissLog) return;
            var local = handLook.transform.InverseTransformPoint(palm);
            var s = handLook.transform.lossyScale;
            float x = local.x * s.x, y = local.y * s.y, z = local.z * Mathf.Max(1e-4f, s.z);
            if (Mathf.Abs(x) > 0.15f || Mathf.Abs(y) > 0.15f || z < -0.12f || z > 0.2f) return;
            m_NextMissLog = Time.unscaledTime + 0.5f;
            Debug.Log($"[RoomOrchestra] Title: palm near the {start.Choice} print but not on it: {x * 100f:F0} cm across, {y * 100f:F0} cm up, {z * 100f:F0} cm behind its face (counts within {printHalfSize.x * Generous * s.x * 100f:F0} x {printHalfSize.y * Generous * s.y * 100f:F0} cm, {-ContactInFront * 100f:F0} to {ContactBehind * 100f:F0} cm deep)");
        }

        readonly Vector3[] m_LastPalm = new Vector3[2];
        readonly bool[] m_HadPalm = new bool[2];

        // how far behind the print's face a point is (m); in front of it, on the player's side, is negative
        float DepthOf(Vector3 point) => handLook.transform.InverseTransformPoint(point).z * Mathf.Max(1e-4f, handLook.transform.lossyScale.z);

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
            float depth = local.z * Mathf.Max(1e-4f, handLook.transform.lossyScale.z);
            return Mathf.Abs(local.x) <= printHalfSize.x * Generous && Mathf.Abs(local.y) <= printHalfSize.y * Generous
                   && depth >= -ContactInFront && depth <= ContactBehind;
        }
    }
}
