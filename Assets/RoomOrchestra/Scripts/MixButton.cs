using TMPro;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// The instrument's name tile under its pitch column, doubling as its volume control: each tap
    /// steps 100% -> 60% -> 30% -> Muted. The change applies at once, also to notes already ringing or
    /// scheduled. Muted instruments keep their echoes (and orbit minis), so unmuting picks the groove
    /// back up mid-bar.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class MixButton : MonoBehaviour
    {
        [SerializeField] InstrumentTarget target;
        [SerializeField] TMP_Text label;

        int m_Level;

        public InstrumentTarget Target => target;
        public TMP_Text Label => label;
        public int Level => m_Level;

        public void Configure(InstrumentTarget instrument, TMP_Text text)
        {
            target = instrument;
            label = text;
        }

        public static string TextFor(InstrumentType instrument, int level, UiLanguage language) =>
            $"{UiText.Name(instrument, language)}\n{UiText.Volume(level, language)}";

        void OnEnable() => Localization.Changed += Refresh;
        void OnDisable() => Localization.Changed -= Refresh;

        void Refresh(UiLanguage language)
        {
            if (label != null && target != null) label.text = TextFor(target.Instrument, m_Level, language);
        }

        public void Press()
        {
            m_Level = MixLevels.Next(m_Level);
            if (target != null) target.SetMixGain(MixLevels.Gain(m_Level));
            Refresh(Localization.Current);
            Debug.Log($"[RoomOrchestra] {(target != null ? target.Instrument.ToString() : name)} volume: {MixLevels.Label(m_Level)}");
        }
    }
}
