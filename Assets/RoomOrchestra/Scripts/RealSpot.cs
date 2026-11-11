using TMPro;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// A glowing spot on a piece of real furniture the room scan found (owner, headset 2026-11-08: "show me visually
    /// where I can assign the furniture to the actual furniture. Right now, there is no indication"). A pulsing pad lies
    /// on the real top and a label floats above it, naming the instrument that lives there ("+" when none does).
    /// Tapping the spot asks <see cref="RealFurniture"/> for the next instrument. One tap, one hand, any distance.
    /// PICO Spatial: every spot exists in the scene and waits shrunk in the ball stand's cloud; it is only moved.
    /// </summary>
    public class RealSpot : MonoBehaviour
    {
        public const float ParkedScale = 0.12f; // the 1.2 cm pad stays above PICO's 1 mm scale limit
        public const float MinSide = 0.22f, MaxSide = 0.7f, PadThickness = 0.012f;
        /// <summary>The label floats this high above the real top.</summary>
        public const float LabelHeight = 0.3f;
        const float PulseSeconds = 1.6f, Pulse = 0.06f, Bob = 0.015f;

        [SerializeField] Transform pad, labelHolder;
        [SerializeField] TMP_Text label;
        [SerializeField] Transform park;
        [SerializeField] Collider tap;

        Vector3 m_PadScale = Vector3.one;
        bool m_Shown;
        string m_Text = "+";

        public bool Shown => m_Shown;
        public string Text => m_Text;
        /// <summary>Which spot of the list this is (RealFurniture sets it).</summary>
        public int Index { get; set; } = -1;

        public void Configure(Transform padPart, Transform labelPart, TMP_Text text, Transform parkPoint, Collider tapBox)
        {
            pad = padPart;
            labelHolder = labelPart;
            label = text;
            park = parkPoint;
            tap = tapBox;
            Park();
        }

        /// <summary>Lies on a real top (centre, footprint in metres) and names what lives there.</summary>
        public void Show(Vector3 top, Vector2 footprint, string text, bool tappable = true)
        {
            m_Shown = true;
            transform.SetPositionAndRotation(top + Vector3.up * 0.005f, Quaternion.identity);
            transform.localScale = Vector3.one;
            m_PadScale = new Vector3(Mathf.Clamp(footprint.x, MinSide, MaxSide), PadThickness, Mathf.Clamp(footprint.y, MinSide, MaxSide));
            if (pad != null) pad.localScale = m_PadScale;
            if (tap != null) tap.enabled = tappable; // a preview of where something will go takes no taps
            SetText(text);
        }

        InstrumentIcon m_Icon;

        /// <summary>The instrument's picture beside its name on the spot (choosing furniture), or none.</summary>
        public void ShowIcon(InstrumentType? type)
        {
            if (type == null)
            {
                if (m_Icon != null) m_Icon.gameObject.SetActive(false);
                return;
            }
            if (m_Icon == null && label != null)
            {
                m_Icon = InstrumentIcon.Create(label.transform.parent != null ? label.transform.parent : label.transform, "InstrumentIcon", 1.4f);
                m_Icon.transform.localPosition = new Vector3(0f, 0.13f, 0f); // just above the name
            }
            if (m_Icon == null) return;
            m_Icon.gameObject.SetActive(true);
            m_Icon.Show(type.Value);
        }

        public void SetText(string text)
        {
            m_Text = text;
            if (label != null && label.text != text) label.text = text;
        }

        public void Hide()
        {
            if (!m_Shown) return;
            m_Shown = false;
            Park();
        }

        void Park()
        {
            m_Shown = false;
            if (tap != null) tap.enabled = false;
            if (labelHolder != null) labelHolder.localPosition = Vector3.zero; // the label tucks into the cloud's core too
            if (park == null) return;
            transform.position = park.position;
            transform.localScale = Vector3.one * ParkedScale;
        }

        void LateUpdate()
        {
            if (!m_Shown)
            {
                // the ball stand travels with the leaf: the parked spot goes with it
                if (park != null && transform.position != park.position)
                    transform.position = park.position;
                return;
            }
            float wave = Mathf.Sin(Time.time * Mathf.PI * 2f / PulseSeconds);
            if (pad != null) pad.localScale = new Vector3(m_PadScale.x * (1f + Pulse * wave), m_PadScale.y, m_PadScale.z * (1f + Pulse * wave));
            if (labelHolder == null) return;
            labelHolder.localPosition = Vector3.up * (LabelHeight + Bob * wave);
            if (Viewer.TryGetPosition(out var eye))
            {
                var away = labelHolder.position - eye;
                away.y = 0f; // upright, turned toward the player
                if (away.sqrMagnitude > 1e-6f) labelHolder.rotation = Quaternion.LookRotation(away, Vector3.up);
            }
        }
    }
}
