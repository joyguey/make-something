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
        [SerializeField] string caption;

        int m_Level;

        public InstrumentTarget Target => target;
        public TMP_Text Label => label;
        public int Level => m_Level;

        public void Configure(InstrumentTarget instrument, TMP_Text text, string name)
        {
            target = instrument;
            label = text;
            caption = name;
        }

        public static string TextFor(string name, int level) => $"{name}\n{MixLevels.Label(level)}";

        public void Press()
        {
            m_Level = MixLevels.Next(m_Level);
            if (target != null) target.SetMixGain(MixLevels.Gain(m_Level));
            if (label != null) label.text = TextFor(caption, m_Level);
            Debug.Log($"[RoomOrchestra] {caption} volume: {MixLevels.Label(m_Level)}");
        }
    }
}
