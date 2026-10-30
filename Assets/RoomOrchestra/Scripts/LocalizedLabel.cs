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
        [Tooltip("The furniture whose instrument this tile (or 1=D) belongs to: it follows when the instrument changes.")]
        [SerializeField] InstrumentTarget owner;
        [Tooltip("Shown only while the owner plays a melodic instrument (the key, 1=D, means nothing on a drum).")]
        [SerializeField] bool melodicOnly;

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

        /// <summary>A pitch tile that follows its furniture's instrument.</summary>
        public void ConfigureTile(TMP_Text label, InstrumentTarget furniture, int index)
        {
            ConfigureTile(label, furniture.Instrument, index);
            owner = furniture;
        }

        /// <summary>A term shown only while <paramref name="furniture"/> plays a melodic instrument.</summary>
        public void ConfigureMelodicOnly(InstrumentTarget furniture)
        {
            owner = furniture;
            melodicOnly = true;
        }

        InstrumentType Instrument => owner != null ? owner.Instrument : instrument;

        public string TextFor(UiLanguage language)
        {
            if (melodicOnly && owner != null && new MusicalNote(owner.Instrument, 0).IsPercussion) return "";
            return isTile ? UiText.Tile(new MusicalNote(Instrument, noteIndex), language) : UiText.Get(term, language);
        }

        void OnEnable()
        {
            Localization.Changed += Apply;
            InstrumentPacks.Changed += Refresh;
            if (text != null && text.text != TextFor(Localization.Current)) Apply(Localization.Current);
        }

        void OnDisable()
        {
            Localization.Changed -= Apply;
            InstrumentPacks.Changed -= Refresh;
        }

        void Refresh() => Apply(Localization.Current);

        void Apply(UiLanguage language)
        {
            if (text != null) text.text = TextFor(language);
        }
    }
}
