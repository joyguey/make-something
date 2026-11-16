using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>Which bare hands PICO is tracking this frame (0 left, 1 right).</summary>
    public static class HandPose
    {
        static readonly int[] s_Frame = { -1, -1 };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_Frame[0] = s_Frame[1] = -1;

        /// <summary><paramref name="hand"/>'s palm is tracked this frame.</summary>
        public static void Report(int hand) => s_Frame[hand] = Time.frameCount;

        public static bool Seen(int hand) => s_Frame[hand] == Time.frameCount;
    }

    /// <summary>
    /// The player's own hands, shown while the meadow stands (owner, headset 2026-11-15: "is there any way there can be
    /// an outline of my hand before I slap it?"). The meadow covers the camera's view of the room, so real hands can't
    /// be seen in it. These are PICO's own hand models (the SDK's HandLeft and HandRight, which follow every joint),
    /// in a look that is solid at the hand's edge and faint inside: an outline of the hand. (A first version drew lines
    /// along the fingers; the owner: "poorly drawn".) Each shows only while its hand is tracked, and both go once the
    /// real room shows, where the real hands are visible.
    /// </summary>
    public class HandOutline : MonoBehaviour
    {
        [SerializeField] GameObject left, right;

        public void Configure(GameObject leftHand, GameObject rightHand)
        {
            left = leftHand;
            right = rightHand;
        }

        public GameObject Model(int hand) => hand == 0 ? left : right;

        /// <summary>Hands are drawn while the meadow hides the real ones.</summary>
        public static bool Wanted => VirtualRoom.Instance == null || !VirtualRoom.Instance.Gone;

        void LateUpdate() => Refresh();

        /// <summary>Shows each hand tracked this frame; hides the rest.</summary>
        public void Refresh()
        {
            bool wanted = Wanted;
            for (int hand = 0; hand < 2; hand++)
            {
                var model = Model(hand);
                if (model == null) continue;
                bool show = wanted && HandPose.Seen(hand);
                if (model.activeSelf != show) model.SetActive(show);
            }
        }
    }
}
