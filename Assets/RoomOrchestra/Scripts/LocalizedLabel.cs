using TMPro;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>A fixed control label (a name, Undo, a pitch tile...) that follows the language.</summary>
    public class LocalizedLabel : MonoBehaviour
    {
        [SerializeField] TMP_Text text;
        [SerializeField] UiTerm term;
        [Tooltip("Pitch tile label: shows this instrument's note or udu sound instead of the term.")]
        [SerializeField] bool isTile;
        [SerializeField] InstrumentType instrument;
        [SerializeField] int noteIndex;

        public TMP_Text Text => text;

        public void Configure(TMP_Text label, UiTerm word)
        {
            text = label;
            term = word;
            isTile = false;
        }

        public void ConfigureTile(TMP_Text label, InstrumentType tileInstrument, int index)
        {
            text = label;
            isTile = true;
            instrument = tileInstrument;
            noteIndex = index;
        }

        public string TextFor(UiLanguage language) => isTile
            ? UiText.Tile(new MusicalNote(instrument, noteIndex), language)
            : UiText.Get(term, language);

        void OnEnable()
        {
            Localization.Changed += Apply;
            if (text != null && text.text != TextFor(Localization.Current)) Apply(Localization.Current);
        }

        void OnDisable() => Localization.Changed -= Apply;

        void Apply(UiLanguage language)
        {
            if (text != null) text.text = TextFor(language);
        }
    }
}
