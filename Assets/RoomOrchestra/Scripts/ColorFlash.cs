using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Briefly tints every renderer under this object, then fades back to the original colors.
    /// Uses per-renderer material instances and <c>_BaseColor</c>/<c>_Color</c>, which PICO Spatial
    /// supports for runtime material changes.
    /// </summary>
    public class ColorFlash : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] Color flashColor = new Color(1f, 0.92f, 0.3f);
        [SerializeField, Min(0.01f)] float duration = 0.3f;

        Material[] m_Materials;
        Color[] m_BaseColors;
        float m_Remaining;

        public bool IsFlashing => m_Remaining > 0f;
        public Color FlashColor => flashColor;

        // Material instances are created on the first flash, not at startup: changes made while
        // PICO's Spatial renderer is still connecting can leave objects drawn wrong.
        void CreateInstances()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            m_Materials = new Material[renderers.Length];
            m_BaseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                m_Materials[i] = renderers[i].material; // instance, so other objects are unaffected
                m_BaseColors[i] = GetColor(m_Materials[i]);
            }
        }

        public void Flash()
        {
            if (ComfortSettings.CalmMotion) return; // calm motion: no flashing
            if (m_Materials == null)
                CreateInstances();
            m_Remaining = duration;
            Apply(1f);
        }

        void Update()
        {
            if (m_Remaining <= 0f)
                return;
            m_Remaining = Mathf.Max(0f, m_Remaining - Time.deltaTime);
            Apply(m_Remaining / duration);
        }

        void Apply(float t)
        {
            for (int i = 0; i < m_Materials.Length; i++)
                SetColor(m_Materials[i], Color.Lerp(m_BaseColors[i], flashColor, t));
        }

        static Color GetColor(Material m)
        {
            if (m.HasProperty(BaseColorId)) return m.GetColor(BaseColorId);
            if (m.HasProperty(ColorId)) return m.GetColor(ColorId);
            return Color.white;
        }

        static void SetColor(Material m, Color c)
        {
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
            else if (m.HasProperty(ColorId)) m.SetColor(ColorId, c);
        }
    }
}
