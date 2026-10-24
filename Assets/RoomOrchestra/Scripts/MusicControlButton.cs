using TMPro;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Tappable Pause/Play or Clear button. Pause silences everything at once and freezes the loop
    /// where it is; tapping again continues from the same spot. Clear removes every echo (Undo brings
    /// them back). The Pause button's label says what a tap will do next.
    /// </summary>
    [RequireComponent(typeof(Collider))]
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

        public Kind ButtonKind => kind;
        public TMP_Text Label => label;

        public void Configure(Kind buttonKind, TMP_Text text)
        {
            kind = buttonKind;
            label = text;
        }

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

        /// <summary>Shows what the next tap does. Text changes only on a real state change.</summary>
        public void Refresh(bool paused)
        {
            if (kind != Kind.Pause || label == null) return;
            string text = paused ? "Play" : "Pause";
            if (label.text != text) label.text = text;
        }
    }
}
