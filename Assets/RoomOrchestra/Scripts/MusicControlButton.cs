using TMPro;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Tappable Pause/Play or Clear button. Pause silences everything at once and freezes the loop
    /// where it is; tapping again continues from the same spot. Clear removes every echo (Undo brings
    /// them back). The Pause button's label and icon (bars / triangle) show what a tap will do next.
    /// The hidden icon is parked inside the slab rather than deactivated: PICO Spatial still draws
    /// inactive objects.
    /// </summary>
    // Its tap collider sits on its visible slab, where PICO's hover highlight can show.
    public class MusicControlButton : MonoBehaviour
    {
        public enum Kind
        {
            Pause,
            Clear
        }

        [SerializeField] Kind kind;
        [Tooltip("Label whose text follows the state (Pause button: 'Pause' / 'Play').")]
        [SerializeField] TMP_Text label;
        [Tooltip("Pause button: icon shown while playing (two bars).")]
        [SerializeField] Transform pauseIcon;
        [Tooltip("Pause button: icon shown while paused (triangle), authored parked inside the slab.")]
        [SerializeField] Transform playIcon;
        [SerializeField] Vector3 iconShown = new Vector3(0f, 0.03f, 0f);
        [SerializeField] Vector3 iconParked = Vector3.zero;

        public static readonly Vector3 DefaultIconShown = new Vector3(0f, 0.03f, 0f);
        public static readonly Vector3 DefaultIconParked = Vector3.zero;

        public Kind ButtonKind => kind;
        public TMP_Text Label => label;
        public Transform PauseIcon => pauseIcon;
        public Transform PlayIcon => playIcon;
        public Vector3 IconShown => iconShown;
        public Vector3 IconParked => iconParked;

        public void Configure(Kind buttonKind, TMP_Text text, Transform pauseBars = null, Transform playTriangle = null)
        {
            kind = buttonKind;
            label = text;
            pauseIcon = pauseBars;
            playIcon = playTriangle;
            iconShown = DefaultIconShown;
            iconParked = DefaultIconParked;
        }

        void OnEnable() => Localization.Changed += Relabel;
        void OnDisable() => Localization.Changed -= Relabel;

        // Clear's label is a LocalizedLabel; Pause's depends on the state too.
        void Relabel(UiLanguage language)
        {
            if (kind == Kind.Pause && label != null) label.text = TextFor(EchoConductor.Instance != null && EchoConductor.Instance.IsPaused, language);
        }

        public static string TextFor(bool paused, UiLanguage language) => UiText.Get(paused ? UiTerm.Play : UiTerm.Pause, language);

        public void Press()
        {
            var conductor = EchoConductor.Instance;
            if (conductor == null) return;
            if (kind == Kind.Clear)
            {
                conductor.Clear();
                return;
            }
            conductor.TogglePause();
            Refresh(conductor.IsPaused);
        }

        /// <summary>Shows what the next tap does. Text and icons change only on a real state change.</summary>
        public void Refresh(bool paused)
        {
            if (kind != Kind.Pause) return;
            string text = TextFor(paused, Localization.Current);
            if (label != null && label.text != text) label.text = text;
            Place(pauseIcon, paused ? iconParked : iconShown);
            Place(playIcon, paused ? iconShown : iconParked);
        }

        static void Place(Transform icon, Vector3 localPosition)
        {
            if (icon != null && icon.localPosition != localPosition) icon.localPosition = localPosition;
        }
    }
}
