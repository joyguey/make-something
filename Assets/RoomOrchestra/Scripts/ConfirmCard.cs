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
        /// <summary>Where the card shows first: right above the button (a touch behind it), so it never blocks a second tap on it.</summary>
        public const float TowardPlayer = -0.02f, Above = 0.2f;
        /// <summary>The card keeps this far from everything else on the panel its button belongs to (m).</summary>
        public const float Clearance = 0.008f;

        // Where the card may stand, nearest choice first: (toward the player, above the button), in metres. On the leaf
        // (seen from above) the first is clear. On an upright panel such as the title sign it would stand inside the sign
        // (owner, headset 2026-11-15: "the dialog box that came up clipped through the sign"), so it steps out in front.
        static readonly (float toward, float above)[] Places =
        {
            (TowardPlayer, Above), (0.06f, 0.17f), (0.10f, 0.17f), (0.14f, 0.17f), (0.18f, 0.17f), (0.14f, 0.24f), (0.22f, 0.24f),
        };

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
            StartOverButton _ => (UiTerm.ConfirmStartOver, UiTerm.ConfirmRestart),
            PlacesButton _ => (UiTerm.ConfirmPlaces, UiTerm.ConfirmRestart),
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
            toEye.Normalize();
            transform.localScale = shownScale;
            // the first place where the card touches nothing else on the button's panel and does not stand between the
            // player and the button (the same button again confirms)
            var others = Neighbours(owner);
            foreach (var (toward, above) in Places)
            {
                Stand(at + toEye * toward + Vector3.up * above, eye);
                if (!Touches(others) && !Hides(owner, eye)) break;
            }
            transform.localScale = shownScale;
            SetTaps(true);
            Debug.Log($"[RoomOrchestra] Confirm card: \"{UiText.Get(m_Question, UiLanguage.English)}\"");
        }

        // Upright, turned only left/right toward the player (headset 2026-11-08: pitched at the eye, the card leaned
        // back and read as "angled downward").
        void Stand(Vector3 place, Vector3 eye)
        {
            var look = place - eye;
            look.y = 0f;
            transform.SetPositionAndRotation(place, Quaternion.LookRotation(look.sqrMagnitude > 1e-6f ? look : Vector3.forward, Vector3.up));
        }

        /// <summary>The card's box in the room, as it stands now.</summary>
        public Bounds Box()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            var box = new Bounds(transform.position, Vector3.zero);
            foreach (var r in renderers) box.Encapsulate(r.bounds);
            return box;
        }

        /// <summary>Everything drawn on the panel <paramref name="owner"/> belongs to (the title sign, the leaf...), the
        /// card and the button's own parts aside.</summary>
        public Renderer[] Neighbours(Component owner)
        {
            var panel = owner.transform;
            while (panel.parent != null && panel.parent.parent != null && panel.parent.name != "LittleEchoes") panel = panel.parent;
            var found = new System.Collections.Generic.List<Renderer>();
            foreach (var r in panel.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || !(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                if (r.transform.IsChildOf(transform) || r.transform.IsChildOf(owner.transform)) continue;
                found.Add(r);
            }
            return found.ToArray();
        }

        /// <summary>The shown card touches nothing on <paramref name="owner"/>'s panel and does not cover it from <paramref name="eye"/> (tests).</summary>
        public bool StandsClear(Component owner, Vector3 eye) => !Touches(Neighbours(owner)) && !Hides(owner, eye);

        bool Touches(Renderer[] others)
        {
            var box = Box();
            box.Expand(Clearance * 2f);
            foreach (var r in others)
                if (r != null && box.Intersects(r.bounds)) return true;
            return false;
        }

        // The card stands on the line from the eyes to the button.
        bool Hides(Component owner, Vector3 eye)
        {
            var to = owner.transform.position - eye;
            return Box().IntersectRay(new Ray(eye, to.normalized), out float hit) && hit < to.magnitude;
        }

        // The ball stand travels with the leaf (and glides in during the opening): the parked card goes with it,
        // written only when the cloud has moved (emulator, 2026-11-07: it was left hanging where the cloud began).
        void LateUpdate()
        {
            if (m_Pending != null || park == null) return;
            if (transform.position != park.position) transform.position = park.position;
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
