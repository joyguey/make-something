using TMPro;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// How much of the room the scan has covered (owner, headset 2026-11-12: "during the scanning process there should
    /// be some indicator of a bar to show how much of the room has been scanned since I don't know when the tutorial
    /// will go to the next step"). A bar a little below eye level ahead of the player, following their gaze slowly,
    /// filling as the scan covers the directions around them, with the percentage written over it. Built in the scene
    /// (its materials ship with the build); waits folded away until the look-around starts.
    /// </summary>
    public class ScanProgressBar : MonoBehaviour
    {
        public const float Distance = 1.1f, BelowEyes = 0.22f, FollowSeconds = 0.6f, BarWidth = 0.42f;

        [SerializeField] Transform fill;
        [SerializeField] TMP_Text label, hint;
        [SerializeField] Vector3 shownScale = Vector3.one;

        bool m_Shown;
        float m_Shown01 = -1f;
        Vector3 m_Velocity;

        public static ScanProgressBar Instance { get; private set; }
        public bool Shown => m_Shown;
        public float Progress => Mathf.Max(0f, m_Shown01);

        public void Configure(Transform fillBar, TMP_Text percent, TMP_Text help)
        {
            fill = fillBar;
            label = percent;
            hint = help;
            shownScale = transform.localScale;
        }

        void Awake()
        {
            Instance = this;
            Hide();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Shows the bar at <paramref name="progress"/> (0..1) of the room scanned.</summary>
        public void Show(float progress)
        {
            progress = Mathf.Clamp01(progress);
            bool arriving = !m_Shown;
            if (!m_Shown)
            {
                m_Shown = true;
                transform.localScale = shownScale;
                foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = true;
            }
            Follow(arriving);
            if (Mathf.Abs(progress - m_Shown01) < 0.001f) return;
            m_Shown01 = progress;
            if (fill != null)
            {
                var s = fill.localScale;
                fill.localScale = new Vector3(Mathf.Max(0.001f, progress), s.y, s.z);
                fill.localPosition = new Vector3(-0.5f + progress * 0.5f, fill.localPosition.y, fill.localPosition.z);
            }
            var language = Localization.Current;
            if (label != null) label.text = string.Format(UiText.Get(UiTerm.ScanProgress, language), Mathf.RoundToInt(progress * 100f));
            if (hint != null) hint.text = UiText.Get(UiTerm.ScanProgressHint, language);
        }

        /// <summary>The scan is good enough: a little flourish, then away.</summary>
        public void Done()
        {
            if (!m_Shown) return;
            PoofCloud.PoofAt(transform.position, 0.5f);
            UiSounds.Play(UiSound.Confirm, transform.position);
            Hide();
        }

        void Hide()
        {
            m_Shown = false;
            m_Shown01 = -1f;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        }

        // Ahead of the player, a little below their eyes, gliding after their gaze (never stuck to the face).
        void Follow(bool snap)
        {
            if (StageAnchor.Instance == null || !StageAnchor.Instance.TryGetHeadPose(out var head)) return;
            var flat = head.rotation * Vector3.forward;
            flat.y = 0f;
            if (flat.sqrMagnitude < 1e-4f) return;
            flat.Normalize();
            var want = head.position + flat * Distance + Vector3.down * BelowEyes;
            transform.position = snap ? want : Vector3.SmoothDamp(transform.position, want, ref m_Velocity, FollowSeconds);
            var look = transform.position - head.position;
            look.y = 0f;
            if (look.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(look);
        }
    }
}
