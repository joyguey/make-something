using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>Where each bare hand's 26 joints are this frame, in tracking (rig) space (PICO's joint order: palm, wrist,
    /// thumb 2..5, index 6..10, middle 11..15, ring 16..20, little 21..25).</summary>
    public static class HandPose
    {
        public const int JointCount = 26;
        static readonly Vector3[][] s_Joints = { new Vector3[JointCount], new Vector3[JointCount] };
        static readonly int[] s_Frame = { -1, -1 };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_Frame[0] = s_Frame[1] = -1;

        /// <summary>This frame's joints for <paramref name="hand"/> (0 left, 1 right).</summary>
        public static void Report(int hand, System.Func<int, Vector3> joint)
        {
            for (int i = 0; i < JointCount; i++) s_Joints[hand][i] = joint(i);
            s_Frame[hand] = Time.frameCount;
        }

        /// <summary>False unless <paramref name="hand"/> was seen this frame.</summary>
        public static bool TryGet(int hand, out Vector3[] joints)
        {
            joints = s_Joints[hand];
            return s_Frame[hand] == Time.frameCount;
        }
    }

    /// <summary>
    /// The player's own hands, drawn as pale shapes while the meadow stands (owner, headset 2026-11-15: "it took me a few
    /// tries to slap the sign around the hand to start it, is there any way there can be an outline of my hand before I
    /// slap it?"). The meadow covers the camera's view of the room, so real hands can't be seen in it; this shows where
    /// each one is: a line along every finger and a wide one across the palm, turned to the eyes so they read as the
    /// hand's shape. Gone once the real room shows (the real hands are visible there).
    /// </summary>
    public class HandOutline : MonoBehaviour
    {
        public const int LinesPerHand = 6;
        public const float FingerWidth = 0.015f, ThumbWidth = 0.018f, PalmWidth = 0.07f;

        // each line's joints: thumb, index, middle, ring, little, and the palm (wrist to the middle knuckle)
        static readonly int[][] Chains =
        {
            new[] { 2, 3, 4, 5 }, new[] { 6, 7, 8, 9, 10 }, new[] { 11, 12, 13, 14, 15 },
            new[] { 16, 17, 18, 19, 20 }, new[] { 21, 22, 23, 24, 25 }, new[] { 1, 12 },
        };

        [SerializeField] LineRenderer[] lines = System.Array.Empty<LineRenderer>(); // left hand's six, then the right's

        public static int JointsIn(int line) => Chains[line].Length;
        public static float WidthOf(int line) => line == 0 ? ThumbWidth : line == 5 ? PalmWidth : FingerWidth;

        public void Configure(LineRenderer[] handLines) => lines = handLines;

        /// <summary>Hands are drawn while the meadow hides the real ones.</summary>
        public static bool Wanted => VirtualRoom.Instance == null || !VirtualRoom.Instance.Gone;

        void LateUpdate() => Refresh();

        /// <summary>Draws each hand seen this frame; hides the rest.</summary>
        public void Refresh()
        {
            var rig = XrRig.Instance != null ? XrRig.Instance.transform : null;
            bool wanted = Wanted;
            for (int hand = 0; hand < 2; hand++)
            {
                bool show = wanted & HandPose.TryGet(hand, out var joints);
                for (int k = 0; k < LinesPerHand; k++)
                {
                    int i = hand * LinesPerHand + k;
                    if (i >= lines.Length || lines[i] == null) continue;
                    var line = lines[i];
                    if (line.enabled != show) line.enabled = show;
                    if (!show) continue;
                    var chain = Chains[k];
                    for (int j = 0; j < chain.Length; j++)
                        line.SetPosition(j, rig != null ? rig.TransformPoint(joints[chain[j]]) : joints[chain[j]]);
                }
            }
        }
    }
}
