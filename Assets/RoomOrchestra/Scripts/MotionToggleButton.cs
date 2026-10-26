using TMPro;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>Tappable Motion toggle (Full / Calm); the label shows the current mode.</summary>
    [RequireComponent(typeof(Collider))]
    public class MotionToggleButton : MonoBehaviour
    {
        [SerializeField] TMP_Text label;

        public TMP_Text Label => label;

        public void Configure(TMP_Text text) => label = text;

        public static string TextFor(MotionMode mode) => $"Motion\n{mode}";

        public void Press()
        {
            ComfortSettings.SetMotion(ComfortSettings.CalmMotion ? MotionMode.Full : MotionMode.Calm);
            if (label != null) label.text = TextFor(ComfortSettings.Motion);
            UnityEngine.Debug.Log($"[RoomOrchestra] Motion: {ComfortSettings.Motion}");
        }
    }
}
