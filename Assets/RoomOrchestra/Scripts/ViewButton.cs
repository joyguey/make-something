using TMPro;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>The menu's Text size button: standard or large labels (high contrast is always on); its label names the size.</summary>
    public class ViewButton : MonoBehaviour
    {
        [SerializeField] TMP_Text label;

        public TMP_Text Label => label;

        public void Configure(TMP_Text text) => label = text;

        public static string TextFor(ViewMode mode, UiLanguage language) =>
            $"{UiText.Get(UiTerm.View, language)}\n{UiText.Get(ViewModes.Term(mode), language)}";

        void OnEnable()
        {
            Localization.Changed += Refresh;
            ViewSettings.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            Localization.Changed -= Refresh;
            ViewSettings.Changed -= Refresh;
        }

        void Refresh(UiLanguage _) => Refresh();

        void Refresh()
        {
            var view = ViewSettings.Instance;
            if (label == null || view == null) return;
            string text = TextFor(view.Mode, Localization.Current);
            if (label.text != text) label.text = text;
        }

        public void Press() => ViewSettings.Instance?.Next();
    }
}
