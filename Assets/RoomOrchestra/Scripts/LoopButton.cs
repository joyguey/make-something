using TMPro;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Menu: the focused instrument (the one the yellow disc and the ball's aim are on) loops what it plays, or plays it
    /// once (owner, headset 2026-11-08: "is there a way to choose when to loop an instrument or not?"). The label names
    /// the instrument and what it does now, so the choice never rests on colour; pressing it switches.
    /// </summary>
    public class LoopButton : MonoBehaviour, IPressable
    {
        TMP_Text m_Text;
        string m_Shown;

        void Awake() => m_Text = GetComponentInChildren<TMP_Text>(true);

        static InstrumentType? Focused()
        {
            var selector = OrbSelector.Instance;
            var target = selector != null ? selector.FocusedTarget : null;
            return target != null ? target.Instrument : null;
        }

        public void Press()
        {
            var focused = Focused();
            if (!focused.HasValue) return;
            EchoConductor.SetLoops(focused.Value, !EchoConductor.Loops(focused.Value));
            Refresh();
        }

        void Update() => Refresh();

        void Refresh()
        {
            if (m_Text == null) return;
            var focused = Focused();
            var language = Localization.Current;
            string text = focused.HasValue
                ? $"{UiText.Name(focused.Value, language)}\n{UiText.Get(EchoConductor.Loops(focused.Value) ? UiTerm.LoopOn : UiTerm.LoopOff, language)}"
                : UiText.Get(UiTerm.LoopOn, language);
            if (text == m_Shown) return;
            m_Shown = text;
            m_Text.text = text;
        }
    }
}
