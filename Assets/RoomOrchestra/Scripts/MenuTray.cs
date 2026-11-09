using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>A control the pointer presses (newer leaf buttons; older ones are routed by type).</summary>
    public interface IPressable
    {
        void Press();
    }

    /// <summary>
    /// The leaf's menu (owner, 2026-11-08: "I think there might be too many buttons on the leaf"): the leaf keeps the
    /// characters, Undo, Pause, Clear and Menu; everything else (Loudness, Sounds, View, language, Height, Hiding, Come
    /// here, Recenter, Places, Lesson, Quit) waits on a tray that Menu opens just above the leaf, tilted toward the
    /// player. Closed, the tray is folded away (shrunk, its colliders off) but stays active, so its buttons keep
    /// following the language and settings. Pause and Clear stay on the leaf, one press away (CLAUDE.md).
    /// </summary>
    public class MenuTray : MonoBehaviour
    {
        public const float ClosedScale = 0.001f;
        public const float OpenSeconds = 0.18f;

        [SerializeField] Vector3 openScale = Vector3.one;
        [SerializeField] bool open;

        Collider[] m_Colliders;
        float m_Animate = -1f;

        public static MenuTray Instance { get; private set; }
        public bool Open => open;

        public static event System.Action<bool> Toggled;

        /// <summary>Saved at its open size (nothing is saved near zero scale); it folds away when the game starts.</summary>
        public void Configure(Vector3 scale)
        {
            openScale = scale;
            open = false;
            transform.localScale = openScale;
        }

        void OnEnable() => Instance = this;

        void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            m_Colliders = GetComponentsInChildren<Collider>(true);
            Apply(instant: true);
        }

        public void Toggle() => SetOpen(!open);

        public void SetOpen(bool on)
        {
            if (open == on) return;
            open = on;
            m_Animate = Time.time;
            Apply(instant: false);
            Toggled?.Invoke(open);
            Debug.Log($"[RoomOrchestra] Menu: {(open ? "open" : "closed")}");
        }

        void Apply(bool instant)
        {
            m_Colliders ??= GetComponentsInChildren<Collider>(true);
            foreach (var c in m_Colliders) if (c != null && c.enabled != open) c.enabled = open;
            if (instant) transform.localScale = openScale * (open ? 1f : ClosedScale);
            Physics.SyncTransforms();
        }

        void Update()
        {
            if (m_Animate < 0f) return;
            float u = Mathf.Clamp01((Time.time - m_Animate) / OpenSeconds);
            if (VineGrowth.ReducedMotion) u = 1f;
            float e = u * u * (3f - 2f * u);
            float s = open ? Mathf.Lerp(ClosedScale, 1f, e) : Mathf.Lerp(1f, ClosedScale, e);
            transform.localScale = openScale * s;
            if (u >= 1f) m_Animate = -1f;
        }
    }

}
