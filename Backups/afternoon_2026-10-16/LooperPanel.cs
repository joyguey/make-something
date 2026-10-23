using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Mixer display for the <see cref="LoopStation"/>: a 16-step playhead strip and one colored
    /// tile per pass. Tap a tile to mute/unmute that pass; tap the block beneath it to remove it.
    /// Tiles pulse when their pass plays a note. All parts are pre-placed primitives (no text).
    /// </summary>
    public class LooperPanel : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] Renderer[] stepPips;
        [SerializeField] Renderer[] passTiles;
        [SerializeField] Renderer[] removeButtons;

        [Header("Colors")]
        [SerializeField] Color pipIdle = new Color(0.16f, 0.17f, 0.2f);
        [SerializeField] Color pipBeat = new Color(0.3f, 0.32f, 0.36f);
        [SerializeField] Color pipNote = new Color(0.45f, 0.75f, 0.85f);
        [SerializeField] Color pipPlayhead = Color.white;
        [SerializeField] Color recordingPlayhead = new Color(1f, 0.2f, 0.15f);
        [SerializeField] Color removeColor = new Color(0.45f, 0.08f, 0.08f);
        [SerializeField, Range(0f, 1f)] float mutedBrightness = 0.22f;
        [SerializeField, Min(0.01f)] float pulseDuration = 0.15f;

        Material[] m_Pips, m_Tiles, m_Removes;
        float[] m_TilePulseUntil;
        LoopStation m_Station;

        void Start()
        {
            m_Pips = Instances(stepPips);
            m_Tiles = Instances(passTiles);
            m_Removes = Instances(removeButtons);
            m_TilePulseUntil = new float[passTiles.Length];
            m_Station = LoopStation.Instance;
            if (m_Station != null)
            {
                m_Station.PassPulsed += OnPassPulsed;
                m_Station.PassesChanged += RefreshVisibility;
            }
            RefreshVisibility();
        }

        void OnDestroy()
        {
            if (m_Station == null) return;
            m_Station.PassPulsed -= OnPassPulsed;
            m_Station.PassesChanged -= RefreshVisibility;
        }

        /// <summary>Called by <see cref="PassSlotButton"/>: mute/unmute or remove the pass in a slot.</summary>
        public void PressSlot(int slot, bool remove)
        {
            if (m_Station == null || slot < 0 || slot >= m_Station.Passes.Count) return;
            var pass = m_Station.Passes[slot];
            if (remove) m_Station.RemovePass(pass);
            else m_Station.ToggleMute(pass);
        }

        void OnPassPulsed(LoopStation.Pass pass)
        {
            int slot = IndexOf(pass);
            if (slot >= 0 && slot < m_TilePulseUntil.Length)
                m_TilePulseUntil[slot] = Time.time + pulseDuration;
        }

        void RefreshVisibility()
        {
            int count = m_Station != null ? m_Station.Passes.Count : 0;
            for (int i = 0; i < passTiles.Length; i++)
            {
                SetActive(passTiles[i], i < count);
                if (i < removeButtons.Length) SetActive(removeButtons[i], i < count);
            }
        }

        void Update()
        {
            if (m_Station == null) return;

            int head = m_Station.CurrentStep;
            int stepsPerBeat = m_Station.StepsPerBeat;
            for (int i = 0; i < m_Pips.Length; i++)
            {
                Color c;
                if (i == head) c = m_Station.IsRecording ? recordingPlayhead : pipPlayhead;
                else if (m_Station.StepHasNotes(i)) c = pipNote;
                else c = i % stepsPerBeat == 0 ? pipBeat : pipIdle;
                Set(m_Pips[i], c);
            }

            var passes = m_Station.Passes;
            for (int i = 0; i < m_Tiles.Length && i < passes.Count; i++)
            {
                var p = passes[i];
                Color c = p.muted ? p.color * mutedBrightness : p.color;
                c.a = 1f;
                if (Time.time < m_TilePulseUntil[i]) c = Color.Lerp(c, Color.white, 0.6f);
                Set(m_Tiles[i], c);
                if (i < m_Removes.Length) Set(m_Removes[i], removeColor);
            }
        }

        int IndexOf(LoopStation.Pass pass)
        {
            var passes = m_Station.Passes;
            for (int i = 0; i < passes.Count; i++)
                if (passes[i] == pass) return i;
            return -1;
        }

        static Material[] Instances(Renderer[] renderers)
        {
            var mats = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                mats[i] = renderers[i] != null ? renderers[i].material : null;
            return mats;
        }

        static void Set(Material m, Color c)
        {
            if (m != null && m.GetColor(BaseColorId) != c) m.SetColor(BaseColorId, c);
        }

        static void SetActive(Renderer r, bool active)
        {
            if (r != null && r.gameObject.activeSelf != active) r.gameObject.SetActive(active);
        }
    }
}
