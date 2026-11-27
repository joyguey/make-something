using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// The meadow theme (V3 review: "some background music"): it plays while there is no music of the player's own,
    /// on the title, through the opening, while the room is scanned and furniture is chosen, and in free play while
    /// the song is empty. It fades out when the lesson starts (the lesson counts in its own beat), as soon as the
    /// player's song has a note, and on Pause; it comes back softly a few seconds after the song is cleared.
    /// Quieter than the instruments; never on top of the player's music.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class BackgroundMusic : MonoBehaviour
    {
        public const float Level = 0.55f, FadeSeconds = 1.2f, ComeBackAfterSeconds = 4f; // owner, headset 2026-11-12: "a little bit faint"
        /// <summary>
        /// Owner, headset 2026-11-12: "the song should only be playing during the silence and disappear once I throw a
        /// ball". A throw or any note fades it out; it comes back only after this long with nothing played.
        /// </summary>
        public const float SilenceSeconds = 8f;
        static float s_LastPlayed = -100f;
        public static void PlayerPlayed() => s_LastPlayed = Time.unscaledTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_LastPlayed = -100f;

        void OnEnable()
        {
            BallLauncher.Thrown += PlayerPlayed;
            MusicalHitPipeline.Committed += OnHit;
        }

        void OnDisable()
        {
            BallLauncher.Thrown -= PlayerPlayed;
            MusicalHitPipeline.Committed -= OnHit;
        }

        static void OnHit(MusicalHitEvent _) => PlayerPlayed();

        [SerializeField] AudioClip theme;

        AudioSource m_Source;
        float m_Volume;

        public static BackgroundMusic Instance { get; private set; }
        /// <summary>Is the theme meant to be heard now (tests)?</summary>
        public bool Wanted { get; private set; }
        public float Volume => m_Volume;

        public void Configure(AudioClip clip) => theme = clip;

        void Awake()
        {
            Instance = this;
            m_Source = GetComponent<AudioSource>();
            m_Source.clip = theme;
            m_Source.loop = true;
            m_Source.playOnAwake = false;
            m_Source.spatialBlend = 0f; // all around, like the meadow
            m_Source.volume = 0f;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Whether the theme should play, from what is going on now.</summary>
        public static bool ShouldPlay()
        {
            // story mode has its own music (owner, headset 2026-11-25: "I'm still hearing the theme song in the back")
            if (StoryDirector.Instance != null && StoryDirector.Instance.Running) return false;
            var title = TitleScreen.Instance;
            if (title != null && title.Showing) return true;
            var intro = IntroSequence.Instance;
            var tutorial = TutorialDirector.Instance;
            bool lesson = tutorial != null && tutorial.Active;
            if (intro != null && !intro.Done) return !lesson;
            if (lesson) return false;
            if (Time.unscaledTime - s_LastPlayed < SilenceSeconds) return false; // the player is playing
            var conductor = EchoConductor.Instance;
            if (conductor != null && (conductor.IsPaused || conductor.Echoes.Count > 0)) return false;
            return true; // the meadow, scanning, choosing furniture, an empty song
        }

        void Update()
        {
            if (theme == null) return;
            bool want = ShouldPlay();
            // after the player's own music stops (Clear), the theme waits a moment before coming back
            if (want && !m_WantedBefore && Time.timeSinceLevelLoad > 1f) m_ComeBackAt = Time.unscaledTime + ComeBackAfterSeconds;
            m_WantedBefore = want;
            Wanted = want && Time.unscaledTime >= m_ComeBackAt;
            float target = Wanted ? Level : 0f;
            m_Volume = Mathf.MoveTowards(m_Volume, target, Time.unscaledDeltaTime * Level / FadeSeconds);
            m_Source.volume = m_Volume;
            if (m_Volume > 0f && !m_Source.isPlaying) m_Source.Play();
            else if (m_Volume <= 0f && m_Source.isPlaying) m_Source.Pause();
        }

        bool m_WantedBefore = true;
        float m_ComeBackAt;
    }
}
