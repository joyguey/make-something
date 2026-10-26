using TMPro;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>Tappable Height toggle (Seated / Standing); the label shows the current setting.</summary>
    [RequireComponent(typeof(Collider))]
    public class HeightButton : MonoBehaviour
    {
        [SerializeField] TMP_Text label;

        public TMP_Text Label => label;

        public void Configure(TMP_Text text) => label = text;

        public static string TextFor(PlayPosture posture) => $"Height\n{posture}";

        public void Press()
        {
            ComfortSettings.SetPosture(PostureLayout.Next(ComfortSettings.Posture));
            if (label != null) label.text = TextFor(ComfortSettings.Posture);
            // Head height is logged (not used) so a headset run can show where the Spatial origin sits.
            string head = StageAnchor.Instance != null && StageAnchor.Instance.TryGetHeadPose(out var pose) ? $"{pose.position.y:F2} m" : "unknown";
            Debug.Log($"[RoomOrchestra] Height: {ComfortSettings.Posture} (controls lifted {PostureLayout.ControlsLift(ComfortSettings.Posture):F2} m, head y {head})");
        }
    }
}
