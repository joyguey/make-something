using TMPro;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Applies the View setting (A11Y-08, <see cref="ViewMode"/>) to the controls: larger labels, and/or high
    /// contrast (near-black slabs, pure white text, bright yellow icons). Materials are swapped per renderer for
    /// runtime copies, so the project's material assets never change. Remembered on the device (local only).
    /// Like everything visible, nothing changes until PICO Spatial's renderer has connected.
    /// </summary>
    public class ViewSettings : MonoBehaviour
    {
        public const string PrefKey = "RoomOrchestra.ViewMode";
        public static readonly Color ContrastSlab = new Color(0.04f, 0.04f, 0.05f), ContrastText = Color.white, ContrastIcon = new Color(1f, 0.86f, 0.2f);

        [Tooltip("Control labels that grow with Large text (pitch tiles and Flutter's bubble already fit their words).")]
        [SerializeField] TMP_Text[] labels;
        [Tooltip("Button and tile slabs: near-black in high contrast.")]
        [SerializeField] Renderer[] slabs;
        [Tooltip("Button icons: bright yellow in high contrast.")]
        [SerializeField] Renderer[] icons;
        [Tooltip("Text that turns pure white in high contrast.")]
        [SerializeField] TMP_Text[] texts;

        ViewMode m_Mode;
        bool m_Applied;
        Vector3[] m_LabelScales;
        Material[] m_SlabMaterials, m_IconMaterials;
        Color[] m_TextColours;
        Material m_ContrastSlab, m_ContrastIcon;

        public static ViewSettings Instance { get; private set; }
        public static event System.Action Changed;
        public ViewMode Mode => m_Mode;
        public System.Collections.Generic.IReadOnlyList<TMP_Text> Labels => labels;
        public System.Collections.Generic.IReadOnlyList<Renderer> Slabs => slabs;
        public System.Collections.Generic.IReadOnlyList<Renderer> Icons => icons;

        public void Configure(TMP_Text[] scalable, Renderer[] slabRenderers, Renderer[] iconRenderers, TMP_Text[] allText)
        {
            labels = scalable;
            slabs = slabRenderers;
            icons = iconRenderers;
            texts = allText;
        }

        void OnEnable() => Instance = this;

        void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        void Awake() => Capture();

        /// <summary>Shows <paramref name="mode"/> at once without remembering it (previews and evidence renders).</summary>
        public void Preview(ViewMode mode)
        {
            Capture();
            m_Mode = mode;
            Apply();
        }

        void Capture()
        {
            if (m_LabelScales != null) return;
            m_LabelScales = new Vector3[labels.Length];
            for (int i = 0; i < labels.Length; i++) m_LabelScales[i] = labels[i] != null ? labels[i].transform.localScale : Vector3.one;
            m_SlabMaterials = new Material[slabs.Length];
            for (int i = 0; i < slabs.Length; i++) m_SlabMaterials[i] = slabs[i] != null ? slabs[i].sharedMaterial : null;
            m_IconMaterials = new Material[icons.Length];
            for (int i = 0; i < icons.Length; i++) m_IconMaterials[i] = icons[i] != null ? icons[i].sharedMaterial : null;
            m_TextColours = new Color[texts.Length];
            for (int i = 0; i < texts.Length; i++) m_TextColours[i] = texts[i] != null ? texts[i].color : Color.white;
            if (Application.isPlaying)
            {
                int saved = PlayerPrefs.GetInt(PrefKey, -1);
                m_Mode = saved < 0 ? ViewModes.Default : ViewModes.WithContrast((ViewMode)Mathf.Clamp(saved, 0, ViewModes.Count - 1));
            }
        }

        void Update()
        {
            // High contrast (and the remembered text size) from the first frame: PICO XR mode draws at once.
            if (m_Applied) return;
            m_Applied = true;
            if (m_Mode != ViewMode.Standard) Apply();
        }

        /// <summary>The View button: the next setting.</summary>
        public void Next() => Set(ViewModes.Next(m_Mode));

        public void Set(ViewMode mode)
        {
            m_Mode = mode;
            m_Applied = true;
            PlayerPrefs.SetInt(PrefKey, (int)mode);
            PlayerPrefs.Save();
            Apply();
            Debug.Log($"[RoomOrchestra] View: {UiText.Get(ViewModes.Term(mode), UiLanguage.English)}");
            Changed?.Invoke();
        }

        void Apply()
        {
            float scale = ViewModes.TextScale(m_Mode);
            for (int i = 0; i < labels.Length; i++)
                if (labels[i] != null) labels[i].transform.localScale = m_LabelScales[i] * scale;
            bool contrast = ViewModes.IsHighContrast(m_Mode);
            if (contrast && m_ContrastSlab == null)
            {
                m_ContrastSlab = Tinted(m_SlabMaterials, ContrastSlab, "ContrastSlab");
                m_ContrastIcon = Tinted(m_IconMaterials, ContrastIcon, "ContrastIcon");
            }
            for (int i = 0; i < slabs.Length; i++)
                if (slabs[i] != null) slabs[i].sharedMaterial = contrast && m_ContrastSlab != null ? m_ContrastSlab : m_SlabMaterials[i];
            for (int i = 0; i < icons.Length; i++)
                if (icons[i] != null) icons[i].sharedMaterial = contrast && m_ContrastIcon != null ? m_ContrastIcon : m_IconMaterials[i];
            for (int i = 0; i < texts.Length; i++)
                if (texts[i] != null) texts[i].color = contrast ? ContrastText : m_TextColours[i];
        }

        static Material Tinted(Material[] from, Color colour, string name)
        {
            Material source = null;
            foreach (var m in from) if (m != null) { source = m; break; }
            if (source == null) return null;
            var tinted = new Material(source) { name = name, color = colour };
            if (tinted.HasProperty("_EmissionColor")) tinted.SetColor("_EmissionColor", colour == ContrastIcon ? colour * 0.4f : Color.black);
            return tinted;
        }
    }
}
