using TMPro;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// The question a press that ends or wipes something asks first (owner, headset 2026-11-07: "the double quit from
    /// the leaf is not self-explanatory"). A small dark card pops up just in front of the pressed button and says
    /// what will happen ("Quit Little Echoes?", "Clear your song?"), with two big buttons: do it, or cancel. Tapping
    /// the same button again still confirms, and pressing anything else cancels.
    /// PICO Spatial: the card exists in the scene and waits shrunk inside the ball stand's cloud; it is only moved.
    /// </summary>
    public class ConfirmCard : MonoBehaviour
    {
        public const float ParkedScale = 0.12f; // the thinnest part (2 cm deep buttons) stays above PICO's near-zero scale limit
        /// <summary>Where the card shows: right above the button (a touch behind it), so it never blocks a second tap on it.</summary>
        public const float TowardPlayer = -0.02f, Above = 0.2f;

        [SerializeField] TMP_Text question, yesText, noText;
        [SerializeField] Transform park;
        [SerializeField] Vector3 shownScale = Vector3.one;
        [SerializeField] Collider[] taps;

        Component m_Pending;
        UiTerm m_Question, m_Yes;

        public static ConfirmCard Instance { get; private set; }
        /// <summary>The press waiting for an answer, or null.</summary>
        public Component Pending => m_Pending;
        public bool Showing => m_Pending != null;
        public string QuestionText => question != null ? question.text : null;

        /// <summary>Wires the card (scene setup) and parks it.</summary>
        public void Configure(TMP_Text questionText, TMP_Text yes, TMP_Text no, Transform parkPoint, Collider[] buttons)
        {
            question = questionText;
            yesText = yes;
            noText = no;
            park = parkPoint;
            taps = buttons;
            shownScale = transform.localScale;
            Park();
        }

        void OnEnable()
        {
            Instance = this;
            Localization.Changed += OnLanguage;
        }

        void OnDisable()
        {
            Localization.Changed -= OnLanguage;
            if (Instance == this) Instance = null;
        }

        /// <summary>What a confirmed press would do, in words: the question and the do-it button.</summary>
        public static (UiTerm question, UiTerm yes) TermsFor(Component owner) => owner switch
        {
            QuitButton _ => (UiTerm.ConfirmQuit, UiTerm.Quit),
            TutorialReplayButton _ => (UiTerm.ConfirmLesson, UiTerm.ConfirmRestart),
            EchoBattleButton _ => (UiTerm.ConfirmLeave, UiTerm.BattleLeave),
            _ => (UiTerm.ConfirmClear, UiTerm.Clear),
        };

        /// <summary>Asks about <paramref name="owner"/>'s press, in front of it.</summary>
        public void Show(Component owner)
        {
            if (owner == null) return;
            m_Pending = owner;
            (m_Question, m_Yes) = TermsFor(owner);
            Write(Localization.Current);
            var at = owner.transform.position;
            var eye = Viewer.TryGetPosition(out var head) ? head : at + owner.transform.forward * -1f + Vector3.up * 0.3f;
            var toEye = eye - at;
            toEye.y = 0f;
            if (toEye.sqrMagnitude < 1e-6f) toEye = -Vector3.forward;
            var place = at + toEye.normalized * TowardPlayer + Vector3.up * Above;
            var look = place - eye;
            transform.SetPositionAndRotation(place, Quaternion.LookRotation(look.sqrMagnitude > 1e-6f ? look : Vector3.forward, Vector3.up));
            transform.localScale = shownScale;
            SetTaps(true);
            Debug.Log($"[RoomOrchestra] Confirm card: \"{UiText.Get(m_Question, UiLanguage.English)}\"");
        }

        /// <summary>No question any more (answered, cancelled, or another press).</summary>
        public void Hide()
        {
            if (m_Pending == null) return;
            m_Pending = null;
            Park();
        }

        void Park()
        {
            SetTaps(false);
            if (park == null) return;
            transform.position = park.position;
            transform.localScale = shownScale * ParkedScale;
        }

        void SetTaps(bool on)
        {
            if (taps == null) return;
            foreach (var c in taps)
                if (c != null) c.enabled = on;
        }

        void OnLanguage(UiLanguage language)
        {
            if (m_Pending != null) Write(language);
        }

        void Write(UiLanguage language)
        {
            if (question != null) question.text = UiText.Get(m_Question, language);
            if (yesText != null) yesText.text = UiText.Get(m_Yes, language);
            if (noText != null) noText.text = UiText.Get(UiTerm.ConfirmCancel, language);
        }
    }
}
