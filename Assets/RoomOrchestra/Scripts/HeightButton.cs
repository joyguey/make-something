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

        public static string TextFor(PlayPosture posture, UiLanguage language) =>
            $"{UiText.Get(UiTerm.Height, language)}\n{UiText.Posture(posture, language)}";

        void OnEnable() => Localization.Changed += Refresh;
        void OnDisable() => Localization.Changed -= Refresh;

        void Refresh(UiLanguage language)
        {
            if (label != null) label.text = TextFor(ComfortSettings.Posture, language);
        }

        public void Press()
        {
            ComfortSettings.SetPosture(PostureLayout.Next(ComfortSettings.Posture));
            Refresh(Localization.Current);
            // Head height is logged (not used) so a headset run can show where the Spatial origin sits.
            string head = StageAnchor.Instance != null && StageAnchor.Instance.TryGetHeadPose(out var pose) ? $"{pose.position.y:F2} m" : "unknown";
            Debug.Log($"[RoomOrchestra] Height: {ComfortSettings.Posture} (controls lifted {PostureLayout.ControlsLift(ComfortSettings.Posture):F2} m, head y {head})");
        }
    }
}
