using TMPro;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>The leaf's Sounds button: the next instrument pack; its label names the pack in use.</summary>
    public class SoundPackButton : MonoBehaviour
    {
        [SerializeField] TMP_Text label;

        public TMP_Text Label => label;

        public void Configure(TMP_Text text) => label = text;

        public static string TextFor(UiTerm pack, UiLanguage language) =>
            $"{UiText.Get(UiTerm.Sounds, language)}\n{UiText.Get(pack, language)}";

        void OnEnable()
        {
            Localization.Changed += Refresh;
            InstrumentPacks.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            Localization.Changed -= Refresh;
            InstrumentPacks.Changed -= Refresh;
        }

        void Refresh(UiLanguage _) => Refresh();

        void Refresh()
        {
            var packs = InstrumentPacks.Instance;
            if (label == null || packs == null || packs.Current == null) return;
            string text = TextFor(packs.Current.name, Localization.Current);
            if (label.text != text) label.text = text;
        }

        public void Press() => InstrumentPacks.Instance?.NextPack();
    }
}
