using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Placeholder control for the <see cref="LoopStation"/>. The record button glows while
    /// recording and pulses on each beat; the clear button flashes when pressed.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class LoopControlButton : MonoBehaviour
    {
        public enum Action
        {
            ToggleRecord,
            Clear
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] Action action;
        [SerializeField] Color idleColor = new Color(0.35f, 0.08f, 0.08f);
        [SerializeField] Color activeColor = new Color(1f, 0.15f, 0.1f);
        [SerializeField, Min(0.01f)] float pulseDuration = 0.12f;

        Renderer m_Renderer;
        Material m_Material; // instanced only when the color first has to change
        float m_PulseUntil;

        public Action ButtonAction => action;

        void Awake()
        {
            m_Renderer = GetComponent<Renderer>();
            Apply(idleColor);
        }

        void OnEnable()
        {
            if (LoopStation.Instance != null) LoopStation.Instance.Beat += OnBeat;
        }

        void Start()
        {
            // LoopStation may wake after this button; subscribe once it exists.
            if (LoopStation.Instance != null)
            {
                LoopStation.Instance.Beat -= OnBeat;
                LoopStation.Instance.Beat += OnBeat;
            }
        }

        void OnDisable()
        {
            if (LoopStation.Instance != null) LoopStation.Instance.Beat -= OnBeat;
        }

        public void Press()
        {
            var station = LoopStation.Instance;
            if (station == null) return;
            if (action == Action.ToggleRecord)
                station.ToggleRecording();
            else
                station.Clear();
            m_PulseUntil = Time.time + pulseDuration * 2f;
        }

        void OnBeat(int beat)
        {
            if (action == Action.ToggleRecord && LoopStation.Instance.IsRecording)
                m_PulseUntil = Time.time + pulseDuration;
        }

        void Update()
        {
            bool recording = LoopStation.Instance != null && LoopStation.Instance.IsRecording;
            bool pulse = Time.time < m_PulseUntil;
            Color c = action == Action.ToggleRecord
                ? (recording ? (pulse ? Color.white : activeColor) : (pulse ? activeColor : idleColor))
                : (pulse ? activeColor : idleColor);
            Apply(c);
        }

        void Apply(Color c)
        {
            // Creating the material instance at startup (while PICO's Spatial renderer is still
            // connecting) can leave objects drawn wrong, so only instance on a real color change.
            var current = m_Material != null ? m_Material : m_Renderer != null ? m_Renderer.sharedMaterial : null;
            if (current == null || !current.HasProperty(BaseColorId) || current.GetColor(BaseColorId) == c)
                return;
            if (m_Material == null) m_Material = m_Renderer.material;
            m_Material.SetColor(BaseColorId, c);
        }
    }
}
