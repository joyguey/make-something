using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoomOrchestra
{
    /// <summary>
    /// Story mode, Level 1: The Seed (owner, 2026-11-22 design; 2026-11-25: "a polished lvl 1 ... an entirely new
    /// feature and not affect the rest of the game"). A portal opens in the player's room with a painted meadow inside;
    /// Discords come through it. The friend on the open left palm is flicked at them with the right index finger:
    /// Flutter pierces Squeaks, Bop ricochets to Wobblers, Boom's shockwave cracks Clangs. The wrong friend makes a
    /// Discord grow. Discords that reach the player get loose in the room and grey it (the mist); full, the level is
    /// lost. Flip the left hand over and back to Hold. Matches fill Harmony (more on the beat); full, the next flick is a
    /// Chorus. Four waves, then the Grumble.
    ///
    /// Separate from the sandbox: everything is built at runtime under its own object. While it plays, the sandbox's
    /// pictures are hidden (only switched off, never changed), its input and song pause; leaving puts them back exactly.
    /// Controllers: point and pull the trigger to launch, left trigger to Hold (no flick needed). Easy aim (saved)
    /// lets a hand pinch aim too, slows the Discords and the mist. Pause and Leave are always there.
    /// </summary>
    public class StoryDirector : MonoBehaviour
    {
        public enum Stage { Off, Intro, WaveCue, Wave, BossCue, Boss, Won, Lost }

        public const float EscapeDistance = 0.95f, HitRadius = 0.13f, PortalAhead = 2.0f;
        public const string EasyPref = "LittleEchoes.Story.Easy";

        public static StoryDirector Instance { get; private set; }
        public Stage Current { get; private set; } = Stage.Off;
        public bool Running => Current != Stage.Off;
        public bool Paused { get; private set; }
        public bool Easy { get; private set; }
        public StoryLevel Level { get; private set; }
        public int WaveIndex { get; private set; }
        public FriendQueue Queue { get; private set; }
        public DiscordMeter Meter { get; private set; }
        public HarmonyMeter Harmony { get; private set; }
        public BossDiscord Boss { get; private set; }
        public int Beaten { get; private set; }
        public int Wrong { get; private set; }
        public float MeterPeak { get; private set; }
        public int Stars { get; private set; }
        public int Alive => m_Discords.Count;
        /// <summary>Sandbox renderers switched off while story plays (for tests).</summary>
        /// <summary>Whether the headset camera is drawing only story mode now (tests).</summary>
        public bool CameraOnStory => m_Camera != null && m_Camera.cullingMask == 1 << StoryArt.Layer;

        sealed class Discord
        {
            public DiscordKind Kind; public Transform Root, Body, Badge; public Transform[] Wings;
            public bool Loose; public float Size = 1f, Born, Seed; public Vector3 Aim;
            // first it walks up the meadow's path inside the portal, then hops out through the opening into the room
            public bool Inside; public float InsideFor; public Vector3 Start, Mid, Cross;
            public List<(Renderer r, Material[] mats)> Back;
        }

        sealed class Note { public Transform T; public Vector3 From, To; public float Born; }

        sealed class Shot
        {
            public OrbType Friend; public Transform Body; public Vector3 Velocity; public float Until, Born;
            public readonly HashSet<object> Hit = new(); public int Bounces; public bool OnBeat, Chorus, HitAny; public object Target;
        }

        sealed class Bit { public Transform T; public Vector3 V; public float Born, Life; public Material M; public Color C; public float Grow; }

        readonly List<Discord> m_Discords = new();
        readonly List<Shot> m_Shots = new();
        readonly List<Bit> m_Bits = new();
        readonly List<(GameObject go, int layer)> m_HandLayers = new();
        Camera m_Camera;
        int m_CameraMask = ~0;
        readonly FlickDetector m_Flick = new();
        readonly PalmFlip m_Flip = new();
        readonly StoryButton[] m_Buttons = new StoryButton[3];
        readonly bool[] m_WasSelect = new bool[2];
        readonly Transform[] m_Wrist = new Transform[FriendQueue.Preview];

        Transform m_Root, m_Hud, m_PalmFriend, m_Held, m_Mist, m_Cursor;
        LineRenderer m_PalmRing, m_HeldRing, m_RightRay;
        StoryArt.Portal m_Portal;
        StoryArt.Grumble m_Grumble;
        TMP_Text m_Title, m_Caption, m_Banner, m_WavePips;
        Material m_MistMat;
        StoryAudio m_Audio;
        InstrumentInputRouter m_Router;
        LeafGrab m_LeafGrab;
        GameObject m_EndStars;
        List<DiscordKind> m_Dealt;
        int m_Spawned;
        float m_T, m_StageAt, m_NextSpawn, m_BannerUntil, m_PortalScale = 1f, m_PortalTarget = 1f, m_NextPalmLog, m_GrumbleHitAt = -9f, m_NextMinion, m_NextForPalm;
        bool m_UsingHands = true, m_ChorusShown, m_HoldTaught; int m_RoomWarned; // hands by default (owner, 2026-11-25)
        Transform m_Card;
        bool m_CardHands, m_CardCollected;
        Vector3 m_Forward, m_Right;
        float m_StageScale = 1f;
        // the story: the meadow's colour (0 grey, 1 full), brought back by each Discord turned into a note
        readonly List<Note> m_Notes = new();
        float m_Bloom, m_BloomTarget, m_Progress, m_GreyPulse, m_WatchFlashUntil = -9f, m_StreakLostUntil = -9f;
        int m_Restored;
        bool m_ChorusBossDone;
        TMP_Text m_BossText;


        int m_LevelIndex;
        Vector3 m_WorldCentre; Quaternion m_WorldFacing;
        /// <summary>Which level (0 = Level 1).</summary>
        public int LevelIndex => m_LevelIndex;
        InstrumentLore Lore => StoryLore.Levels[m_LevelIndex];

        // Story's words in the player's language (StoryText; owner, 2026-11-26: Chinese for story mode)
        static string T(string english) => StoryText.In(english, Localization.Current);
        static string F(string english, params object[] args) => string.Format(T(english), args);
        static bool Chinese => Localization.Current == UiLanguage.Chinese;
        static string FriendName(OrbType f) => UiText.Get(f == OrbType.Flutter ? UiTerm.Flutter : f == OrbType.Bop ? UiTerm.Bop : UiTerm.Boom, Localization.Current);
        static string InstrumentName(InstrumentLore lore) => Chinese ? UiText.Name(lore.Instrument, UiLanguage.Chinese) : lore.Name;
        string TheInstrument => Chinese ? UiText.Name(Lore.Instrument, UiLanguage.Chinese) : $"The {Lore.Name}";
        string CardIntro => F("{0} from {1}.", T(Lore.Kind), T(Lore.People));
        bool Restored(int index) => PlayerPrefs.GetInt(StoryLore.RestoredKey(StoryLore.Levels[index].Instrument), 0) == 1;
        /// <summary>A level is open once the one before it has restored its instrument (story order only; the sandbox is never locked).</summary>
        bool Open(int index) => index == 0 || (index < StoryLore.Levels.Length && Restored(index - 1));
        float m_FlashRingUntil = -9f, m_LeaveAskedUntil = -9f, m_LastFlickAt = -9f;
        Vector3 m_FriendAt = Vector3.one * 999f;
        const float LeaveAskSeconds = 3f;
        const float InsideSeconds = 3.4f, NoteSeconds = 0.8f;
        StoryDiorama m_World;
        readonly List<Transform> m_AimDots = new();
        readonly List<(Transform t, Vector3 home, float seed, Material m)> m_Motes = new();
        Material m_AimMat;
        float m_PopAt = -9f, m_FlashUntil = -9f;
        const int AimDotCount = 14;
        Transform m_Watch;
        LineRenderer m_WatchFill;
        const float WatchRadius = 0.04f;
        float m_NextHandLog;
        TMP_Text m_Streak;
        int m_LastStreakShown;
        // Level 1 is gentle: the room fills slowly (headset 2026-11-25: lost in 30 s); later levels less so
        float MeterAssist => m_LevelIndex == 0 ? (Easy ? 3f : 1.6f) : (Easy ? 2.4f : 1.25f);

        // ---------- start-up: one director per scene that has the sandbox ----------
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Ensure();

        static void Ensure()
        {
            if (FindFirstObjectByType<StoryDirector>() != null || FindFirstObjectByType<InstrumentInputRouter>() == null) return;
            new GameObject("StoryDirector").AddComponent<StoryDirector>();
        }

        void OnEnable() => Instance = this;
        void OnDisable() { if (Instance == this) Instance = null; }

        void Start()
        {
            Easy = PlayerPrefs.GetInt(EasyPref, 0) == 1;
            StoryArt.PrepareFriends(transform);
            m_Audio = new GameObject("StoryAudio").AddComponent<StoryAudio>();
            m_Audio.transform.SetParent(transform, false);
        }

        // ---------- begin and end ----------
        /// <summary>Story chosen on the title card: begin, leaving the sandbox's lesson for the first time the player
        /// plays the sandbox (owner, 2026-11-26: "Straight into Story").</summary>
        public void BeginFromTitle() { Begin(skipLesson: false); m_FromTitle = Running; }

        /// <summary>Story was chosen on the title card: Leave goes back to the card.</summary>
        bool m_FromTitle;

        public void Begin() => Begin(skipLesson: true);

        void Begin(bool skipLesson)
        {
            if (Running) return;
            if (EchoBattleDirector.Instance != null && EchoBattleDirector.Instance.Shown) return; // not during a battle
            var lesson = TutorialDirector.Instance;
            if (skipLesson && lesson != null && lesson.Active) lesson.Skip();
            MenuTray.Instance?.SetOpen(false);
            StoryArt.PrepareFriends(transform); // before hiding the sandbox: copies come from the leaf's friends
            m_Router = FindFirstObjectByType<InstrumentInputRouter>();
            m_LeafGrab = FindFirstObjectByType<LeafGrab>();
            if (m_LeafGrab != null) m_LeafGrab.enabled = false;
            if (m_Router != null) m_Router.enabled = false; // the sandbox's taps pause; its pointer source is still read here
            EchoConductor.Instance?.SetSongMuted(true);
            HideSandbox();
            m_LevelIndex = 0;
            for (int i = 0; i < StoryLore.Levels.Length; i++) if (Open(i)) m_LevelIndex = i;   // the furthest level reached
            for (int i = 0; i < StoryLore.Levels.Length; i++) if (Open(i) && !Restored(i)) { m_LevelIndex = i; break; } // or the first still to mend
            BuildStage();
            StartLevel();
            Debug.Log($"[RoomOrchestra] Story: started Level {m_LevelIndex + 1} (easy aim {(Easy ? "on" : "off")}; camera on story only: {CameraOnStory})");
        }

        /// <summary>Leave story mode: everything back as it was.</summary>
        public void Leave(string why = "left")
        {
            if (!Running) return;
            Debug.Log($"[RoomOrchestra] Story: {why} (stage {Current}, wave {WaveIndex + 1}, beaten {Beaten}, wrong {Wrong}, Discord peak {MeterPeak:F2})");
            if (Current == Stage.Won) SaveRestored(); // won but not collected: it is still theirs
            Current = Stage.Off;
            Paused = false;
            m_Audio.Stop();
            ClearLevel();
            m_World?.Destroy(); m_World = null;
            m_AimDots.Clear(); m_Motes.Clear();
            if (m_Root != null) Destroy(m_Root.gameObject);
            m_Root = null;
            ShowSandbox();
            if (m_LeafGrab != null) m_LeafGrab.enabled = true;
            if (m_Router != null) m_Router.enabled = true;
            EchoConductor.Instance?.SetSongMuted(false);
            if (m_FromTitle) { m_FromTitle = false; TitleScreen.Instance?.ShowAgain(); } // back to the sign post to choose
        }

        void OnApplicationPause(bool paused) { if (paused && Running && !Paused && Current != Stage.Won && Current != Stage.Lost) SetPaused(true); }

        void OnDestroy() { if (Running) ShowSandbox(); }

        // The sandbox is hidden by the camera, not by touching it: while story plays the headset camera draws only story
        // mode's own layer (StoryArt.Layer) and the player's hands (moved onto it for the while). The room scan's depth
        // walls are on their own layer, so they stop hiding the level too. Leaving restores the camera and the hands.
        void HideSandbox()
        {
            m_Camera = Camera.main;
            if (m_Camera == null)
                foreach (var c in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (c.CompareTag("MainCamera")) { m_Camera = c; break; }
            if (m_Camera != null) { m_CameraMask = m_Camera.cullingMask; m_Camera.cullingMask = 1 << StoryArt.Layer; }
            m_HandLayers.Clear();
            var hands = GameObject.Find("PlayerHands");
            if (hands == null) foreach (var o in FindObjectsByType<HandOutline>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { hands = o.gameObject; break; }
            if (hands != null)
                foreach (var t in hands.GetComponentsInChildren<Transform>(true)) { m_HandLayers.Add((t.gameObject, t.gameObject.layer)); t.gameObject.layer = StoryArt.Layer; }
        }

        void ShowSandbox()
        {
            if (m_Camera != null) m_Camera.cullingMask = m_CameraMask;
            m_Camera = null;
            foreach (var (go, layer) in m_HandLayers) if (go != null) go.layer = layer;
            m_HandLayers.Clear();
        }

        // ---------- building the stage ----------
        void BuildStage()
        {
            m_Root = new GameObject("StoryStage").transform;
            m_Root.SetParent(transform, false);
            var head = Head();
            m_Forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (m_Forward.sqrMagnitude < 1e-4f) m_Forward = Vector3.forward;
            m_Forward.Normalize();
            m_Right = Vector3.Cross(Vector3.up, m_Forward);
            // in front of the nearest real wall or furniture straight ahead, never inside it
            float ahead = PortalAhead;
            if (Physics.Raycast(head.position, m_Forward, out var wall, PortalAhead + 0.4f, 1 << RealWalls.SurfaceLayer, QueryTriggerInteraction.Ignore))
                ahead = Mathf.Clamp(wall.distance - 0.4f, 1.5f, PortalAhead);
            m_StageScale = ahead / PortalAhead;
            var centre = head.position + m_Forward * ahead + Vector3.up * 0.15f * m_StageScale;
            Debug.Log($"[RoomOrchestra] Story: portal {ahead:F2} m ahead{(ahead < PortalAhead ? " (a wall is near)" : "")}");
            var facing = Quaternion.LookRotation(m_Forward);
            m_Portal = StoryArt.BuildPortal(m_Root);
            m_Portal.Root.SetPositionAndRotation(centre, facing);
            m_WorldCentre = centre; m_WorldFacing = facing; // the world behind the opening is built for each level (StartLevel)
            // fireflies drifting in the room, like the preview's (soft, slow, never toward the face)
            var random = new System.Random(8);
            for (int i = 0; i < 28; i++)
            {
                var dir = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f) * Vector3.forward;
                var home = head.position + dir * (0.9f + (float)random.NextDouble() * 1.8f) + Vector3.up * ((float)random.NextDouble() * 1.3f - 0.7f);
                var m = StoryArt.Glow(new Color(1f, 0.97f, 0.85f, 0.8f), transparent: true, queueOffset: 12);
                var mote = StoryArt.Part(PrimitiveType.Sphere, m_Root, Vector3.zero, Vector3.one * (0.008f + 0.006f * (float)random.NextDouble()), m, "Firefly");
                mote.transform.position = home;
                m_Motes.Add((mote.transform, home, (float)random.NextDouble() * 100f, m));
            }
            // the aim line: glowing dots from the friend on the palm to a Discord it can beat (the preview's dotted trail)
            m_AimMat = StoryArt.Glow(new Color(1f, 0.97f, 0.88f, 0.85f), transparent: true, queueOffset: 13);
            for (int i = 0; i < AimDotCount; i++)
            {
                var dot = StoryArt.Part(PrimitiveType.Sphere, m_Root, Vector3.zero, Vector3.one * 0.007f, m_AimMat, "AimDot");
                dot.SetActive(false);
                m_AimDots.Add(dot.transform);
            }
            m_Hud = new GameObject("Hud").transform;
            m_Hud.SetParent(m_Root, false);
            m_Hud.SetPositionAndRotation(centre + m_Forward * -0.03f, facing);
            m_Hud.localScale = Vector3.one * m_StageScale;
            var plum = new Color(0.227f, 0.176f, 0.333f);
            var cream = new Color(0.98f, 0.95f, 0.88f);
            m_Title = StoryArt.Words(m_Hud, "Level 1: The Seed", new Vector3(0f, 1.06f, 0f), 0.08f, cream, 2f);
            m_WavePips = StoryArt.Words(m_Hud, "", new Vector3(0f, 0.97f, 0f), 0.045f, cream, 2f);
            m_Banner = StoryArt.Words(m_Hud, "", new Vector3(0f, 0.87f, 0f), 0.065f, new Color(1f, 0.9f, 0.5f), 2.2f);
            m_Caption = StoryArt.Words(m_Hud, "", new Vector3(0f, -0.92f, 0f), 0.05f, cream, 1.9f);
            StoryArt.Words(m_Hud, T("Harmony"), new Vector3(0.98f, 0f, 0f), 0.04f, new Color(1f, 0.82f, 0.4f), 0.5f);
            StoryArt.Words(m_Hud, T("Discord"), new Vector3(-0.98f, 0f, 0f), 0.04f, new Color(0.75f, 0.72f, 0.82f), 0.5f);
            // the buttons, under the caption
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                m_Buttons[i] = StoryButton.Create(m_Hud, new Vector3((i - 1) * 0.36f, -1.16f, 0f), cream, plum, () => Press(k), $"StoryButton_{i}");
            }
            // the mist: a grey veil round the player, thicker as Discord fills the room
            var mist = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(mist.GetComponent<Collider>());
            mist.name = "DiscordMist"; mist.layer = StoryArt.Layer; m_Mist = mist.transform; m_Mist.SetParent(m_Root, false); m_Mist.localScale = Vector3.one * 3.2f;
            m_MistMat = StoryArt.Glow(new Color(0.42f, 0.41f, 0.46f, 0f), transparent: true, queueOffset: 40, cullFront: true);
            mist.GetComponent<MeshRenderer>().sharedMaterial = m_MistMat;
            // the palm: a ring where the hand is seen, the friend above it, the next two by the wrist, the held one
            m_PalmRing = Ring(m_Root, 0.045f, 0.006f, new Color(1f, 0.95f, 0.8f, 0.8f));
            m_HeldRing = Ring(m_Root, 0.03f, 0.004f, new Color(0.8f, 0.9f, 1f, 0.8f));
            m_RightRay = new GameObject("ControllerRay") { layer = StoryArt.Layer }.AddComponent<LineRenderer>();
            m_RightRay.transform.SetParent(m_Root, false);
            m_RightRay.positionCount = 2; m_RightRay.widthMultiplier = 0.004f; m_RightRay.useWorldSpace = true;
            m_RightRay.sharedMaterial = StoryArt.Glow(new Color(1f, 1f, 1f, 0.5f), transparent: true);
            // the Harmony watch round the right wrist (headset 2026-11-25: "wraps around my right hand like a wristwatch
            // that is being charged up ... there's too much going on on the left hand"): a dark band with a gold ring
            // that charges round it with every match, faster on the beat and in a streak; the streak shows above it
            m_Watch = new GameObject("HarmonyWatch") { layer = StoryArt.Layer }.transform; m_Watch.SetParent(m_Root, false);
            StoryArt.Circle(m_Watch, "Band", WatchRadius, 0.016f, new Color(0.2f, 0.17f, 0.26f, 0.6f), 4);
            m_WatchFill = StoryArt.Circle(m_Watch, "Charge", WatchRadius, 0.011f, new Color(1f, 0.8f, 0.3f), 6, 0f, 0.001f, 48);
            m_WatchFill.transform.localPosition = new Vector3(0f, 0f, -0.001f);
            m_Streak = StoryArt.Words(m_Root, "", Vector3.zero, 0.022f, new Color(1f, 0.88f, 0.5f), 0.3f);
            m_Streak.textWrappingMode = TextWrappingModes.NoWrap;
            var cursor = StoryArt.Part(PrimitiveType.Sphere, m_Root, Vector3.zero, Vector3.one * 0.018f, StoryArt.Glow(new Color(1f, 0.95f, 0.7f)), "Cursor");
            m_Cursor = cursor.transform; cursor.SetActive(false);
        }

        static LineRenderer Ring(Transform parent, float radius, float width, Color c)
        {
            var lr = new GameObject("Ring") { layer = StoryArt.Layer }.AddComponent<LineRenderer>();
            lr.transform.SetParent(parent, false);
            lr.loop = true; lr.positionCount = 32; lr.useWorldSpace = false; lr.widthMultiplier = width;
            lr.alignment = LineAlignment.TransformZ;
            lr.sharedMaterial = StoryArt.Glow(c, transparent: true, queueOffset: 5);
            lr.startColor = lr.endColor = c;
            for (int i = 0; i < 32; i++) { float a = i / 32f * Mathf.PI * 2f; lr.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius); }
            return lr;
        }

        /// <summary>The world behind the opening (StoryDiorama), seen only through it: the meadow by day for Level 1, at
        /// dusk on the way to the mountains for Level 2.</summary>
        void BuildWorld()
        {
            m_World?.Destroy();
            m_World = new StoryDiorama();
            m_World.Build(m_Root, dusk: m_LevelIndex >= 1, night: m_LevelIndex >= 2);
            m_World.Root.SetPositionAndRotation(m_WorldCentre, m_WorldFacing);
            m_World.Root.localScale = Vector3.one * m_StageScale;
            m_World.AddInstrument(Lore.Instrument);
        }

        void StartLevel()
        {
            ClearLevel();
            Level = StoryLevel.All[Mathf.Clamp(m_LevelIndex, 0, StoryLevel.All.Length - 1)];
            BuildWorld();
            if (m_Title != null) m_Title.text = F("Level {0}: {1}", Level.Number, T(Level.Name));
            m_Audio.SetRepairRole(Lore.Role);
            Queue = new FriendQueue(System.Environment.TickCount);
            Meter = new DiscordMeter(MeterAssist);
            Harmony = new HarmonyMeter();
            m_LastStreakShown = 0;
            Boss = null;
            Beaten = Wrong = 0; MeterPeak = 0f; Stars = 0; WaveIndex = 0;
            m_T = 0f; m_PortalScale = 0.2f; m_PortalTarget = 0.7f; m_ChorusShown = false; m_HoldTaught = false; m_RoomWarned = 0;
            m_Restored = 0; m_Bloom = m_BloomTarget = 0.12f; m_Progress = 0f; m_GreyPulse = 0f;
            foreach (var n in m_Notes) if (n.T != null) Destroy(n.T.gameObject);
            m_Notes.Clear();
            StoryDiorama.SetBloom(m_Bloom);
            m_World?.SetProgress(0f);
            for (int i = 0; i < 2; i++) m_WasSelect[i] = true; // the press that started the story doesn't launch or Hold
            m_Flick.Lost(); m_Flip.Reset();
            Paused = false;
            m_Audio.Begin(Level.Bpm);
            m_Audio.Layers = 0;
            m_Audio.SetSour(0f);
            RefreshHand();
            if (m_EndStars != null) Destroy(m_EndStars);
            SetStage(Stage.Intro);
            m_Banner.text = "";
            m_CardCollected = false;
            Say("");
            ShowIntroCard();
            Labels();
        }

        void ClearLevel()
        {
            foreach (var d in m_Discords) if (d.Root != null) Destroy(d.Root.gameObject);
            foreach (var s in m_Shots) if (s.Body != null) Destroy(s.Body.gameObject);
            foreach (var b in m_Bits) if (b.T != null) Destroy(b.T.gameObject);
            m_Discords.Clear(); m_Shots.Clear(); m_Bits.Clear();
            if (m_Grumble != null && m_Grumble.Root != null) Destroy(m_Grumble.Root.gameObject);
            m_Grumble = null;
        }

        void SetStage(Stage s)
        {
            Current = s;
            m_StageAt = m_T;
            if (s == Stage.WaveCue)
            {
                var wave = Level.Waves[WaveIndex];
                m_Dealt = StoryLevel.Deal(wave, System.Environment.TickCount + WaveIndex);
                m_Spawned = 0;
                m_Audio.Layers = WaveIndex >= 1 ? 1 : 0;
                Say((WaveIndex > 0 ? T("The friends walk on toward the World Tree... ") : "") + T(wave.Text ?? Cue(wave.Cue)));
                m_Banner.text = F("Wave {0}", WaveIndex + 1);
                m_BannerUntil = m_T + 2.5f;
            }
            Pips();
        }

        string Cue(StoryCue cue) => cue switch
        {
            StoryCue.MeetSqueaks => "Squeaks! Tiny, spiky and fast. Flutter (the tall one) flies straight through a whole line of them. Every Discord you beat becomes a note that brings colour back to the meadow.",
            StoryCue.MeetWobblers => Hands()
                ? "Wobblers! They sway about. Bop (the round one) bounces off your walls to reach them. Not the friend you need? Flip your left hand over and back to swap it with your held friend."
                : "Wobblers! They sway about. Bop (the round one) bounces off your walls to reach them. Not the friend you need? Pull the left trigger to swap it with your held friend.",
            StoryCue.MeetClangs => "Clangs! They wear armour. Only Boom (the wide one) can crack it with a big shockwave.",
            StoryCue.AllTogether => "Here they all come! Each Discord carries a little picture of the friend that beats it.",
            StoryCue.BossComing => m_LevelIndex == 0
                ? "The World Tree is close... but the Grumble blocks the way! Its shell shows which friend can crack it: spikes for Flutter, jelly for Bop, armour for Boom. Crack all three shells!"
                : m_LevelIndex == 1 ? "The Grumble's mountain hideout! It has grown a fourth shell, and each takes five hits. Watch which friend each shell needs!"
                : "The Grumble's last stand at the World Tree: five shells, five hits each. Everything you've learned, all at once!",
            _ => "",
        };

        // ---------- the instrument's card ----------
        // Before the stage: the instrument, its story and how to play, and Begin (owner, 2026-11-25: introduce it "in
        // the beginning before the stage starts"). After a win: its card to collect, with all its real history.

        Vector3 CardSpot => m_Portal != null ? m_Portal.Root.position - m_Forward * 0.35f : Head().position + m_Forward;

        void PlaceCard(Transform card)
        {
            card.SetPositionAndRotation(CardSpot, Quaternion.LookRotation(m_Forward));
            card.localScale = Vector3.one * m_StageScale;
        }

        void ClearCard() { if (m_Card != null) Destroy(m_Card.gameObject); m_Card = null; }

        string HowToPlay() => T(Hands()
            ? "HOW TO PLAY\nHold your left hand open: a friend sits on your palm. Curl your right index finger against your thumb beside it, then flick it at a Discord. Each Discord shows the friend that beats it. Flip your left hand over and back to swap friends."
            : "HOW TO PLAY\nA friend sits by your left controller. Point your right controller at a Discord and pull the trigger. Each Discord shows the friend that beats it. Pull the left trigger to swap friends.");

        void ShowIntroCard()
        {
            ClearCard();
            m_CardHands = Hands();
            var body = CardIntro + "\n\n" + F("{0} Help Flutter, Bop and Boom mend it!", T(Lore.Broken)) + "\n\n" + HowToPlay();
            m_Card = StoryArt.Card(m_Root, Lore.Instrument, F("Level {0}: {1}", m_LevelIndex + 1, TheInstrument), body, 0.9f, 1.2f);
            StoryButton.Create(m_Card, new Vector3(0f, -0.68f, -0.01f), StoryArt.CardGold, StoryArt.CardInk, BeginLevel, "CardButton").SetLabel(T("Begin"));
            // the other levels reached so far, either side of Begin
            if (m_LevelIndex > 0)
                StoryButton.Create(m_Card, new Vector3(-0.36f, -0.68f, -0.01f), StoryArt.CardFace, StoryArt.CardInk, () => ChooseLevel(m_LevelIndex - 1), "LevelButton").SetLabel(F("< Level {0}", m_LevelIndex));
            if (Open(m_LevelIndex + 1))
                StoryButton.Create(m_Card, new Vector3(0.36f, -0.68f, -0.01f), StoryArt.CardFace, StoryArt.CardInk, () => ChooseLevel(m_LevelIndex + 1), "LevelButton").SetLabel(F("Level {0} >", m_LevelIndex + 2));
            // the instrument cards collected so far (a bonus, never a lock), each opens to read again
            var collected = new List<int>();
            for (int i = 0; i < StoryLore.Levels.Length; i++) if (Restored(i)) collected.Add(i);
            for (int k = 0; k < collected.Count; k++)
            {
                int which = collected[k];
                StoryButton.Create(m_Card, new Vector3((k - (collected.Count - 1) / 2f) * 0.36f, -0.82f, -0.01f), StoryArt.CardGold, StoryArt.CardInk, () => ShowCollectedCard(which), "CollectedCard")
                    .SetLabel(F("{0} card", InstrumentName(StoryLore.Levels[which])));
            }
            PlaceCard(m_Card);
        }

        bool NextReady => m_LevelIndex + 1 < StoryLevel.All.Length;

        void ChooseLevel(int index)
        {
            if (index < 0 || index >= StoryLevel.All.Length || !Open(index) || Current != Stage.Intro) return;
            UiSounds.Play(UiSound.Select, CardSpot);
            m_LevelIndex = index;
            StartLevel();
            Debug.Log($"[RoomOrchestra] Story: chose Level {index + 1}");
        }

        void BeginLevel()
        {
            if (Current != Stage.Intro) return;
            UiSounds.Play(UiSound.Confirm, CardSpot);
            ClearCard();
            Debug.Log("[RoomOrchestra] Story: Begin");
            SetStage(Stage.WaveCue);
        }

        void ShowCollectCard()
        {
            ClearCard();
            m_Card = StoryArt.Card(m_Root, Lore.Instrument, TheInstrument, T("Restored! Collect its card to read its story."), 0.55f, 0.62f, bodySize: 0.04f);
            StoryButton.Create(m_Card, new Vector3(0f, -0.22f, -0.02f), StoryArt.CardGold, StoryArt.CardInk, CollectCard, "CardButton").SetLabel(T("Collect")); // on the card, clear of the ring
            PlaceCard(m_Card);
        }

        string FullCardText() => FullCardText(Lore);

        static string FullCardText(InstrumentLore lore) =>
            F("{0} from {1}.", T(lore.Kind), T(lore.People)) + "\n\n" + string.Join("\n\n", System.Linq.Enumerable.Select(lore.Facts, T));

        /// <summary>A collected card, opened from the intro card; Close goes back to it.</summary>
        void ShowCollectedCard(int index)
        {
            if (Current != Stage.Intro) return;
            UiSounds.Play(UiSound.Open, CardSpot);
            ClearCard();
            var lore = StoryLore.Levels[index];
            m_Card = StoryArt.Card(m_Root, lore.Instrument, Chinese ? UiText.Name(lore.Instrument, UiLanguage.Chinese) : $"The {lore.Name}", FullCardText(lore), 1.05f, 1.42f, bodySize: 0.034f);
            StoryButton.Create(m_Card, new Vector3(0f, -0.8f, -0.01f), StoryArt.CardGold, StoryArt.CardInk, () => { UiSounds.Play(UiSound.Close, CardSpot); ShowIntroCard(); }, "CardButton").SetLabel(T("Close"));
            PlaceCard(m_Card);
            m_Card.position += Vector3.up * 0.12f * m_StageScale;
        }

        void CollectCard()
        {
            UiSounds.Play(UiSound.Place, CardSpot);
            SaveRestored();
            ClearCard();
            m_Card = StoryArt.Card(m_Root, Lore.Instrument, TheInstrument, FullCardText(), 1.05f, 1.42f, bodySize: 0.034f);
            StoryButton.Create(m_Card, new Vector3(0f, -0.8f, -0.01f), StoryArt.CardGold, StoryArt.CardInk, CloseCard, "CardButton").SetLabel(T("Close"));
            PlaceCard(m_Card);
            m_Card.position += Vector3.up * 0.12f * m_StageScale;
            // the card has the stage to itself while it is open
            m_SaidBeforeCard = m_Caption.text; Say("");
            if (m_EndStars != null) m_EndStars.SetActive(false);
            m_Banner.text = "";
        }

        string m_SaidBeforeCard = "";

        void CloseCard()
        {
            UiSounds.Play(UiSound.Close, CardSpot);
            ClearCard();
            if (m_EndStars != null) m_EndStars.SetActive(true);
            Say(m_SaidBeforeCard);
            Banner(T("Card collected! Find it on Story's first card."), 4f);
        }

        void SaveRestored()
        {
            if (m_CardCollected) return;
            m_CardCollected = true;
            bool first = PlayerPrefs.GetInt(StoryLore.RestoredKey(Lore.Instrument), 0) == 0;
            PlayerPrefs.SetInt(StoryLore.RestoredKey(Lore.Instrument), 1); // a bonus only: nothing in the sandbox is ever locked
            PlayerPrefs.Save();
            Debug.Log($"[RoomOrchestra] Story: the {Lore.Name} card collected{(first ? " (first time)" : "")}");
        }


        void Pips()
        {
            if (Level == null || m_WavePips == null) return;
            m_WavePips.text = Current switch
            {
                Stage.Intro => T("Get ready"),
                Stage.BossCue or Stage.Boss => T("The Grumble"),
                Stage.Won => T("Complete!"),
                Stage.Lost => F("Wave {0} of {1}", WaveIndex + 1, Level.Waves.Count),
                _ => F("Wave {0} of {1}, then the Grumble", WaveIndex + 1, Level.Waves.Count),
            };
        }

        bool Hands() => m_UsingHands;

        void Say(string text) { if (m_Caption != null) m_Caption.text = text; }
        void Banner(string text, float seconds = 2f) { if (m_Banner != null) { m_Banner.text = text; m_BannerUntil = m_T + seconds; } }

        // ---------- buttons: Pause / Resume / Try again, Easy aim, Leave ----------
        void Labels()
        {
            if (m_Buttons[0] == null) return;
            m_Buttons[0].SetLabel(T(Current == Stage.Won ? (NextReady ? "Next level" : "Play again") : Current == Stage.Lost ? "Try again" : Paused ? "Resume" : "Pause"));
            m_Buttons[1].SetLabel(T(Easy ? "Easy aim: on" : "Easy aim: off"));
            m_Buttons[2].SetLabel(T(Time.time < m_LeaveAskedUntil ? "Sure? Leave" : "Leave"));
        }

        void Press(int which)
        {
            UiSounds.Play(UiSound.Press, m_Buttons[which].transform.position);
            if (which == 0)
            {
                if (Current == Stage.Won && NextReady) { SaveRestored(); m_LevelIndex++; StartLevel(); }
                else if (Current == Stage.Won || Current == Stage.Lost) StartLevel();
                else SetPaused(!Paused);
            }
            else if (which == 1)
            {
                Easy = !Easy;
                PlayerPrefs.SetInt(EasyPref, Easy ? 1 : 0);
                if (Current != Stage.Won && Current != Stage.Lost)
                {
                    float level = Meter.Level;
                    Meter = new DiscordMeter(MeterAssist);
                    if (level > 0f) Meter.Tick(level / DiscordMeter.PerSecondEach * Meter.Assist, 1); // keep how grey the room is
                }
                Banner(T(Easy ? "Easy aim on: slower Discords, stronger aim help" : "Easy aim off"), 2.5f);
            }
            else if (Time.time < m_LeaveAskedUntil) Leave("left with the Leave button");
            else
            {
                // asks first, like the sandbox's Clear and Quit (headset 2026-11-25: a flick pressed Leave by accident)
                m_LeaveAskedUntil = Time.time + LeaveAskSeconds;
                Banner(T("Press Leave again to go back to the sandbox"), LeaveAskSeconds);
            }
            Labels();
        }

        public void SetPaused(bool paused)
        {
            if (!Running || Current == Stage.Won || Current == Stage.Lost) return;
            Paused = paused;
            m_Audio.Paused = paused;
            if (paused) { m_SaidBeforePause = m_Caption.text; Say(T("Paused. Nothing moves until you press Resume.")); }
            else Say(m_SaidBeforePause);
            Labels();
        }

        string m_SaidBeforePause = "";
        StoryButton m_HoverButton;
        float m_HoverSince;
        /// <summary>During play, how long the eyes rest on a button before a pinch presses it (s).</summary>
        public const float ButtonSettle = 0.3f;

        // ---------- each frame ----------
        void Update()
        {
            if (!Running) return;
            float dt = Time.deltaTime;
            var pointers = m_Router != null ? m_Router.Pointers : null;
            var rig = XrRig.Instance != null ? XrRig.Instance.transform : null;
            PointerFrame left = pointers != null ? pointers.Read(0) : default, right = pointers != null ? pointers.Read(1) : default;
            var head = Head();
            m_UsingHands = left.Joints || right.Joints || (!left.Tracked && !right.Tracked && m_UsingHands);
            Vector3 W(Vector3 p) => rig != null ? rig.TransformPoint(p) : p;
            Vector3 D(Vector3 d) => rig != null ? rig.TransformDirection(d) : d;
            Quaternion Q(Quaternion q) => rig != null ? rig.rotation * q : q;


            // pointing at the buttons (eyes and pinch for hands, the controller's ray otherwise)
            StoryButton hovered = null; int hoveredBy = -1;
            for (int hand = 0; hand < 2; hand++)
            {
                var f = hand == 0 ? left : right;
                if (!f.Tracked) continue;
                var origin = W(f.Position); var dir = Q(f.Rotation) * Vector3.forward;
                var b = StoryButton.Under(origin, dir, out var point);
                if (b != null && (hovered == null || hand == 1)) { hovered = b; hoveredBy = hand; m_Cursor.position = point; }
            }
            foreach (var b in m_Buttons) if (b != null) b.SetHover(b == hovered);
            m_Cursor.gameObject.SetActive(hovered != null);
            bool rightController = right.Tracked && !right.Joints && !right.Gaze;
            m_RightRay.enabled = rightController;
            if (rightController) { var o = W(right.Position); m_RightRay.SetPosition(0, o); m_RightRay.SetPosition(1, o + Q(right.Rotation) * Vector3.forward * 1.5f); }

            bool leftPress = left.Select && !m_WasSelect[0], rightPress = right.Select && !m_WasSelect[1];
            m_WasSelect[0] = left.Select; m_WasSelect[1] = right.Select;
            // a hand's pinch presses a button only when that hand is away from the friend and hasn't just flicked: a flick
            // brushes thumb and finger together (headset 2026-11-25: a flick, eyes on the button row, pressed Leave)
            // only while flicking is on (headset 2026-11-25: the intro card's Begin was blocked by a hand resting by the friend)
            bool flicking = !Paused && (Current == Stage.WaveCue || Current == Stage.Wave || Current == Stage.BossCue || Current == Stage.Boss);
            // During play a pinch presses a button the eyes have rested on (owner, headset 2026-11-26: "it was hard to
            // select the buttons with my pinch ... when I wanted to switch between easy aim on and off"): either hand's
            // pinch works once the button has been looked at for a moment, unless a flick was just made or the right
            // finger is loaded right at the friend. A flick's brush of thumb and finger, eyes passing the row, stays
            // safe: the look must settle on the button first.
            if (hovered != m_HoverButton) { m_HoverButton = hovered; m_HoverSince = Time.time; }
            bool settled = hovered != null && Time.time - m_HoverSince >= ButtonSettle;
            bool loadedAtFriend = right.Joints && Vector3.Distance(W(right.IndexTip), m_FriendAt) < 0.1f;
            bool handsBusy = flicking && (!settled || loadedAtFriend || Time.time - m_LastFlickAt < 0.6f);
            if (handsBusy && ((hoveredBy == 1 && right.Joints) || (hoveredBy == 0 && left.Joints))) { leftPress = false; rightPress = false; }
            if (hovered != null && ((hoveredBy == 0 && leftPress) || (hoveredBy == 1 && rightPress)))
            {
                hovered.Press();
                if (!Running) return;
                if (hoveredBy == 0) leftPress = false; else rightPress = false;
            }

            AnimatePortal(dt);
            UpdateBanner();
            if (m_Card != null) { var model = m_Card.Find("CardModel"); if (model != null) model.Rotate(0f, 40f * Time.deltaTime, 0f, Space.Self); }
            if (Current == Stage.Intro && m_Card != null && m_CardHands != Hands()) ShowIntroCard(); // controllers picked up: their instructions
            if (m_LeaveAskedUntil > 0f && Time.time >= m_LeaveAskedUntil) { m_LeaveAskedUntil = -9f; Labels(); }
            if (Paused) { PlaceHand(left, head, W, D); return; }
            m_T += dt;
            float now = m_T;

            // the hand: the friend above the left palm, and the aim line from it
            var friendAt = PlaceHand(left, head, W, D);
            m_FriendAt = friendAt;
            UpdateAimLine(friendAt);
            PlaceWatch(right, head, W, Q);
            // Hold: turn the left hand over and back (hands); the left trigger (controllers)
            if (Current != Stage.Won && Current != Stage.Lost)
            {
                bool hold = false;
                if (left.Joints)
                {
                    m_Flip.Settle(now);
                    hold = m_Flip.Step(D(left.PalmUp), now);
                    if (Time.time >= m_NextPalmLog) { m_NextPalmLog = Time.time + 4f; Debug.Log($"[RoomOrchestra] Story: left palm axis height {m_Flip.Height:F2}"); }
                }
                else if (leftPress && left.Tracked) hold = true;
                if (hold) DoHold(friendAt);

                // flick: the right index finger loaded against the thumb beside the friend, then snapping out
                if (right.Joints)
                {
                    var hand = new FlickHand { Tip = W(right.IndexTip), Thumb = W(right.ThumbTip), Palm = W(right.GripPosition), Friend = friendAt };
                    if (m_Flick.Step(hand, now, dt, out var dir)) { m_LastFlickAt = Time.time; Launch(friendAt, dir, "flick"); }
                    if (Time.time >= m_NextHandLog && Vector3.Distance(hand.Tip, friendAt) < 0.35f)
                    {
                        m_NextHandLog = Time.time + 1.5f;
                        Debug.Log($"[RoomOrchestra] Story: right hand by the friend: finger {m_Flick.Curl * 100f:F0} cm from palm, {m_Flick.Gap * 100f:F0} cm from thumb, tip {Vector3.Distance(hand.Tip, friendAt) * 100f:F0} cm from friend, snap {m_Flick.Speed:F2} m/s, ready {m_Flick.Near}");
                    }
                }
                else m_Flick.Lost();
                if (m_Flick.TakeMiss(out var peak)) Debug.Log($"[RoomOrchestra] Story: finger loaded by the friend, no flick: fastest snap {peak:F2} m/s (a flick needs {FlickDetector.MinSnap:F2})");
                // controllers: point and pull the trigger (a hand's pinch never launches: a flick brushes thumb and
                // finger together, and the pinch fired a second friend, headset 2026-11-25)
                if (rightPress && rightController)
                {
                    var from = W(right.Position);
                    var aim = Q(right.Rotation) * Vector3.forward;
                    Launch(friendAt, (from + aim * 3f - friendAt).normalized, "aim");
                }
            }

            RunStage(now, head.position);
            MoveDiscords(head.position, dt, now);
            MoveGrumble(head.position, now);
            MoveShots(dt, now, head.position);
            MoveBits(now, dt);
            MoveNotes(now);
            m_Bloom = Mathf.MoveTowards(m_Bloom, m_BloomTarget, dt * 0.2f);
            StoryDiorama.SetBloom(m_Bloom);
            float repair = Mathf.Clamp01((m_Bloom - 0.12f) / 0.88f); // the instrument mends as the meadow's colour returns
            m_World?.SetInstrument(repair);
            m_Audio.SetRepair(repair);

            // the room's Discord meter: the mist, the arc, the music souring
            int loose = 0; foreach (var d in m_Discords) if (d.Loose) loose++;
            if (Current != Stage.Won && Current != Stage.Lost) Meter.Tick(dt, loose);
            MeterPeak = Mathf.Max(MeterPeak, Meter.Level);
            // the room greys as loose Discords fill it: the earlier look (owner, headset 2026-11-25: "I liked how it looked
            // before, it was similar to static"); a short pulse when one escapes
            m_GreyPulse = Mathf.MoveTowards(m_GreyPulse, 0f, dt * 0.4f);
            float greyness = Current == Stage.Won ? 0f : Mathf.Clamp01(Meter.Level * 0.55f + m_GreyPulse * 0.5f);
            m_MistMat.color = new Color(0.42f, 0.41f, 0.46f, greyness);
            m_Mist.position = head.position;
            m_Audio.SetSour(Meter.Level);
            // a clear warning before the room fills (owner, headset 2026-11-26: "Did I automatically fail the level
            // because too many enemies piled up in my room?"): at half full and nearly full, said in words
            if (Current != Stage.Won && Current != Stage.Lost)
            {
                if (Meter.Level < 0.35f) m_RoomWarned = 0;
                else if (Meter.Level >= 0.8f && m_RoomWarned < 2) { m_RoomWarned = 2; Banner(T("Your room is nearly full of Discord! Beat the ones in your room!"), 3.5f); m_Audio.Play(StorySound.SqueakCall, Head().position, 0.6f); }
                else if (Meter.Level >= 0.5f && m_RoomWarned < 1) { m_RoomWarned = 1; Banner(T("Discord is filling your room! Beat the ones that got in."), 3f); }
            }
            if (Meter.Lost && Current != Stage.Lost && Current != Stage.Won) Lose();
        }

        Vector3 PlaceHand(PointerFrame left, Transform head, System.Func<Vector3, Vector3> W, System.Func<Vector3, Vector3> D)
        {
            Vector3 palm;
            bool seen = left.Joints || left.BarePalm || (left.Tracked && !left.Gaze);
            if (left.Joints || left.BarePalm) palm = W(left.GripPosition);
            else if (left.Tracked && !left.Gaze) palm = W(left.Position);
            else palm = head.position + m_Forward * 0.35f - Vector3.up * 0.3f - m_Right * 0.12f;
            var friendAt = palm + Vector3.up * 0.085f;
            float bob = Mathf.Sin(Time.time * 3f) * 0.005f;
            float beat = m_Audio.BeatPhase();
            float pulse = 1f + 0.25f * Mathf.Exp(-beat * 6f);
            if (m_PalmFriend != null)
            {
                m_PalmFriend.position = friendAt + Vector3.up * bob;
                Face(m_PalmFriend, head.position);
                // the next friend pops in after a flick; it swells a little when the finger is close enough to flick it
                float pop = Mathf.Clamp01((Time.time - m_PopAt) / 0.3f);
                float grow = pop >= 1f ? 1f : 1f + 0.25f * Mathf.Sin(pop * Mathf.PI) * (1f - pop) + (pop - 1f);
                float ready = m_Flick.Near ? 1.18f : 1f;
                m_PalmFriend.localScale = Vector3.one * 0.07f * Mathf.Max(0.05f, grow) * ready;
            }
            m_PalmRing.transform.position = palm + Vector3.up * 0.02f;
            m_PalmRing.transform.rotation = Quaternion.LookRotation(Vector3.up);
            m_PalmRing.widthMultiplier = 0.006f * pulse * (Harmony != null && Harmony.Full ? 1.8f : 1f);
            bool flash = Time.time < m_FlashUntil;
            var ringColor = flash ? Color.white : Harmony != null && Harmony.Full ? new Color(1f, 0.8f, 0.3f, 0.9f) : m_Flick.Near ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 0.95f, 0.8f, seen ? 0.6f : 0.25f);
            if (m_Flick.Near || flash) m_PalmRing.widthMultiplier *= flash ? 2.4f : 1.6f;
            m_PalmRing.startColor = m_PalmRing.endColor = ringColor;
            // the next friends line up along the forearm, wrist first (headset 2026-11-25: "oriented along the rest of
            // my wrist/arm"); without a tracked wrist, toward the player
            var wrist = left.Joints ? W(left.Wrist) : palm - m_Forward * 0.06f;
            var arm = wrist - palm; if (arm.sqrMagnitude < 1e-5f) arm = -m_Forward; arm.Normalize();
            for (int i = 0; i < m_Wrist.Length; i++)
                if (m_Wrist[i] != null) { m_Wrist[i].position = wrist + arm * (0.015f + 0.05f * i) + Vector3.up * 0.045f; Face(m_Wrist[i], head.position); }
            var heldAt = palm - m_Right * 0.085f + Vector3.up * 0.03f;
            m_HeldRing.gameObject.SetActive(m_Held != null || Queue != null);
            m_HeldRing.transform.position = heldAt;
            m_HeldRing.transform.rotation = Quaternion.LookRotation(heldAt - head.position);
            if (m_Held != null) { m_Held.position = heldAt; Face(m_Held, head.position); }
            return friendAt;
        }

        /// <summary>Turns a friend's face (its -z side) toward the player.</summary>
        /// <summary>The Harmony watch round the right wrist: the gold charge goes round as Harmony fills.</summary>
        void PlaceWatch(PointerFrame right, Transform head, System.Func<Vector3, Vector3> W, System.Func<Quaternion, Quaternion> Q)
        {
            if (m_Watch == null || Harmony == null) return;
            bool seen = right.Joints || (right.Tracked && !right.Gaze);
            m_Watch.gameObject.SetActive(seen);
            m_Streak.gameObject.SetActive(seen);
            if (!seen) return;
            Vector3 wrist, axis; // axis: along the forearm, toward the hand
            if (right.Joints) { wrist = W(right.Wrist); axis = W(right.GripPosition) - wrist; }
            else { axis = Q(right.Rotation) * Vector3.forward; wrist = W(right.Position) - axis * 0.1f; }
            if (axis.sqrMagnitude < 1e-6f) axis = m_Forward;
            axis.Normalize();
            var up = Vector3.ProjectOnPlane(Vector3.up, axis); if (up.sqrMagnitude < 1e-4f) up = Vector3.ProjectOnPlane(head.up, axis);
            m_Watch.SetPositionAndRotation(wrist - axis * 0.015f, Quaternion.LookRotation(axis, up) * Quaternion.Euler(0f, 0f, 180f)); // the charge starts at the top
            float h = Mathf.Clamp01(Harmony.Level);
            StoryArt.SetArc(m_WatchFill, WatchRadius, 0f, Mathf.Max(0.002f, h));
            var gold = new Color(1f, 0.8f, 0.3f);
            var c = Time.time < m_WatchFlashUntil ? new Color(0.95f, 0.35f, 0.4f) : Harmony.Full ? Color.Lerp(gold, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.time * 8f)) : gold;
            m_WatchFill.startColor = m_WatchFill.endColor = c;
            m_WatchFill.widthMultiplier = Harmony.Full ? 0.016f : 0.011f;
            m_Streak.transform.position = wrist + up.normalized * 0.065f;
            m_Streak.transform.rotation = Quaternion.LookRotation(m_Streak.transform.position - head.position);
            m_Streak.text = Time.time < m_StreakLostUntil ? T("streak lost") : Harmony.Full ? T("CHORUS!") : Harmony.Streak >= 2 ? F("x{0} streak", Harmony.Streak) : "";
        }

        static void Emote(Transform friend, Expression e)
        {
            if (friend != null && friend.TryGetComponent<CharacterFace>(out var face)) face.Show(e);
        }

        static void Face(Transform t, Vector3 head)
        {
            var d = t.position - head; d.y = 0f;
            if (d.sqrMagnitude > 1e-4f) t.rotation = Quaternion.LookRotation(d);
        }

        void DoHold(Vector3 at)
        {
            if (Queue.Hold())
            {
                UiSounds.Play(UiSound.Open, at);
                Emote(m_PalmFriend, RoomOrchestra.Expression.Curious);
                Puff(at, 0.25f);
                RefreshHand();
                Debug.Log($"[RoomOrchestra] Story: Hold, {Queue.Held} tucked away, {Queue.Current} on the palm");
            }
            else Banner(T("Flick first, then Hold again"), 1.5f);
        }

        /// <summary>A flick shows: a white ring bursts from the palm, sparkles, and the next friend pops in.</summary>
        void FlickBurst(Vector3 at, OrbType friend)
        {
            m_FlashUntil = Time.time + 0.18f;
            m_PopAt = Time.time;
            var ring = StoryArt.Part(PrimitiveType.Sphere, m_Root, Vector3.zero, Vector3.one * 0.05f, StoryArt.Glow(new Color(1f, 1f, 1f, 0.5f), transparent: true, queueOffset: 30), "FlickRing");
            ring.transform.position = at;
            var m = ring.GetComponent<MeshRenderer>().sharedMaterial;
            m_Bits.Add(new Bit { T = ring.transform, Born = m_T, Life = 0.25f, M = m, C = m.color, Grow = 0.16f });
            Burst(at, friend, 5);
        }

        /// <summary>Where the aim line points: the nearest Discord the palm friend beats (or the Grumble, if its shell
        /// is that friend's).</summary>
        /// <summary>Little Discords at once during the boss fight.</summary>
        const int MaxBossMinions = 4;

        bool HasDiscordFor(OrbType friend)
        {
            foreach (var d in m_Discords) if (d.Root != null && StoryRules.Matches(friend, d.Kind)) return true;
            return false;
        }

        Vector3? AimTarget(Vector3 from)
        {
            if (Queue == null) return null;
            var friend = Queue.Current;
            Vector3? best = null; float bestD = float.MaxValue;
            foreach (var d in m_Discords)
            {
                if (!StoryRules.Matches(friend, d.Kind) || d.Root == null || d.Inside) continue;
                float dist = Vector3.Distance(from, d.Root.position);
                if (dist < bestD) { bestD = dist; best = d.Root.position; }
            }
            if (m_Grumble != null && Boss != null && !Boss.Defeated && StoryRules.Matches(friend, Boss.Shell))
            {
                float dist = Vector3.Distance(from, m_Grumble.Root.position);
                if (dist < bestD) best = m_Grumble.Root.position;
            }
            return best;
        }

        void UpdateAimLine(Vector3 from)
        {
            var target = Current == Stage.Won || Current == Stage.Lost ? null : AimTarget(from);
            bool show = target.HasValue && m_PalmFriend != null && Time.time - m_PopAt > 0.3f;
            for (int i = 0; i < m_AimDots.Count; i++)
            {
                var dot = m_AimDots[i];
                if (dot.gameObject.activeSelf != show) dot.gameObject.SetActive(show);
                if (!show) continue;
                // a gentle arc, the dots flowing toward the Discord; nearer dots shown, far ones fading out
                float f = ((i + Time.time * 1.6f) % AimDotCount) / AimDotCount;
                var a = from; var b = target.Value;
                var mid = (a + b) * 0.5f + Vector3.up * (0.12f + 0.08f * Vector3.Distance(a, b));
                var p = Vector3.Lerp(Vector3.Lerp(a, mid, f), Vector3.Lerp(mid, b, f), f);
                dot.position = p;
                dot.localScale = Vector3.one * (m_Flick.Near ? 0.011f : 0.007f) * (1f - 0.5f * f);
            }
        }

        void RefreshHand()
        {
            Replace(ref m_PalmFriend, Queue.Current, 0.07f);
            for (int i = 0; i < m_Wrist.Length; i++) Replace(ref m_Wrist[i], Queue.Next[i], 0.032f);
            if (Queue.Held.HasValue) Replace(ref m_Held, Queue.Held.Value, 0.04f);
            else if (m_Held != null) { Destroy(m_Held.gameObject); m_Held = null; }
        }

        void Replace(ref Transform slot, OrbType friend, float size)
        {
            if (slot != null) Destroy(slot.gameObject);
            slot = StoryArt.Friend(friend, size, m_Root);
        }

        // ---------- the level's flow ----------
        void RunStage(float now, Vector3 head)
        {
            float since = now - m_StageAt;
            switch (Current)
            {
                case Stage.Intro:
                    break; // the intro card waits for Begin
                    break;
                case Stage.WaveCue:
                    if (since > 4.5f) { SetStage(Stage.Wave); m_NextSpawn = now; }
                    break;
                case Stage.Wave:
                {
                    var wave = Level.Waves[WaveIndex];
                    float beat = 60f / (float)Level.Bpm;
                    if (m_Spawned < wave.Count && now >= m_NextSpawn)
                    {
                        Spawn(m_Dealt[m_Spawned], null);
                        m_Spawned++;
                        m_NextSpawn = now + wave.BeatsApart * beat * (Easy ? 1.6f : 1f);
                    }
                    if (m_Spawned >= wave.Count && m_Discords.Count == 0)
                    {
                        Banner(T("Wave cleared!"), 2f);
                        UiSounds.Play(UiSound.Confirm, m_Portal.Root.position);
                        WaveIndex++;
                        if (WaveIndex < Level.Waves.Count) SetStage(Stage.WaveCue);
                        else if (Level.Boss) { SetStage(Stage.BossCue); Say(T(Cue(StoryCue.BossComing))); m_Audio.Layers = 2; }
                        else Win();
                    }
                    break;
                }
                case Stage.BossCue:
                    if (since > 1.5f && m_Grumble == null) SummonGrumble();
                    if (since > 5f) { SetStage(Stage.Boss); m_NextMinion = now + 6f; }
                    break;
                case Stage.Boss:
                    if (m_Grumble == null || Boss == null || Boss.Defeated) break;
                    // Every friend has something to beat (owner, headset 2026-11-26: "the player is forced to flick off
                    // the wrong ones away anyways ... maybe there needs to be an increase number of enemies so that the
                    // randomization of the friends on the wrist makes sense"): when the friend on the palm can't crack
                    // the shell and no little Discord it beats is about, the Grumble spits one out for it. The aim
                    // help then takes each friend to its own target: the shell, or the little Discord.
                    var palm = Queue.Current;
                    if (!StoryRules.Matches(palm, Boss.Shell) && !HasDiscordFor(palm) && now >= m_NextForPalm && m_Discords.Count < MaxBossMinions)
                    {
                        Spawn(StoryRules.BeatenBy(palm), m_Grumble.Root.position);
                        m_NextForPalm = now + (Easy ? 1.6f : 1.1f);
                        m_NextMinion = Mathf.Max(m_NextMinion, now + 2f);
                    }
                    else if (now >= m_NextMinion && m_Discords.Count < MaxBossMinions)
                    {
                        // and a little Discord now and then besides (more often than before)
                        Spawn((DiscordKind)Random.Range(0, 3), m_Grumble.Root.position);
                        m_NextMinion = now + 6f * (60f / (float)Level.Bpm) * (Easy ? 1.5f : 1f);
                    }
                    break;
            }
        }

        void UpdateBanner()
        {
            if (m_Banner != null && m_Banner.text.Length > 0 && m_T > m_BannerUntil && Current != Stage.Won && Current != Stage.Lost) m_Banner.text = "";
            if (Harmony != null && Harmony.Full && !m_ChorusShown && Running && Current != Stage.Won && Current != Stage.Lost)
            {
                m_ChorusShown = true;
                Banner(T(Hands() ? "Harmony full! Your next flick is a CHORUS!" : "Harmony full! Your next shot is a CHORUS!"), 3f);
                m_Audio.Play(StorySound.Chorus, Head().position, 0.6f);
            }
        }

        void AnimatePortal(float dt)
        {
            if (m_Portal == null) return;
            // the way to the World Tree opens wider as the meadow's colour comes back; wide for the Grumble; widest at the end
            if (Current == Stage.Intro || Current == Stage.WaveCue || Current == Stage.Wave) m_PortalTarget = 0.7f + 0.45f * m_Bloom;
            m_PortalScale = Mathf.MoveTowards(m_PortalScale, m_PortalTarget, dt * 0.5f);
            m_Portal.Root.localScale = Vector3.one * m_PortalScale * m_StageScale;
            float beat = m_Audio.BeatPhase();
            m_Portal.Ring.widthMultiplier = 0.035f * (1f + 0.35f * Mathf.Exp(-beat * 5f)) * (Time.time < m_FlashRingUntil ? 2.2f : 1f); // pulses on the beat, flashes as a note arrives
            // the journey: the friends walk on between waves (the meadow slides past, the World Tree comes nearer) and
            // stop to face the Discords while a wave is on
            float travel = Paused || Current == Stage.Lost ? 0f : Current == Stage.Won ? 0.35f : Current == Stage.Intro || Current == Stage.WaveCue ? 0.22f : 0f;
            bool frightened = !Paused && (Current == Stage.Wave || Current == Stage.BossCue || Current == Stage.Boss); // stopped by the Discords
            m_World?.Scroll(travel, dt, frightened);
            if (Level != null)
            {
                float want = Current == Stage.Won ? 1f : (WaveIndex + (Current == Stage.BossCue || Current == Stage.Boss ? 1f : 0f)) / (Level.Waves.Count + 1f);
                m_Progress = Mathf.MoveTowards(m_Progress, want, dt * 0.06f);
                m_World?.SetProgress(m_Progress);
            }
            float grey = m_MistMat != null ? m_MistMat.color.a : 0f;
            // fireflies drift and twinkle
            foreach (var (t, home, seed, m) in m_Motes)
            {
                if (t == null) continue;
                float k = Time.time * 0.25f + seed;
                // as Discord greys the room the fireflies turn to grey static: flickering, jittering specks
                float g = Mathf.Clamp01((grey - 0.08f) * 4f);
                var jitter = g > 0f ? Random.insideUnitSphere * 0.03f * g : Vector3.zero;
                t.position = home + new Vector3(Mathf.Sin(k * 1.3f), Mathf.Sin(k * 0.9f + 2f) * 0.6f, Mathf.Cos(k * 1.1f)) * 0.12f + jitter;
                float a = Mathf.Lerp(0.35f + 0.55f * Mathf.Max(0f, Mathf.Sin(Time.time * 1.7f + seed * 3f)), Random.value * 0.9f, g);
                var c = Color.Lerp(new Color(1f, 0.97f, 0.85f), new Color(0.5f, 0.5f, 0.55f), g); c.a = a; m.color = c;
            }
            for (int i = 0; i < m_Portal.Leaves.Length; i++)
                m_Portal.Leaves[i].localRotation *= Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 1.3f + i) * 8f * dt);
            if (Harmony != null) StoryArt.SetArc(m_Portal.HarmonyArc, 0.575f, 0.05f, 0.05f + 0.4f * Mathf.Max(0.002f, Harmony.Level));
            if (Meter != null) StoryArt.SetArc(m_Portal.DiscordArc, 0.575f, 0.95f - 0.4f * Mathf.Max(0.002f, Meter.Level), 0.95f);
        }

        // ---------- Discords ----------
        void Spawn(DiscordKind kind, Vector3? from)
        {
            var root = StoryArt.Discord(kind, m_Root, out var body, out var wings);
            var d = new Discord { Kind = kind, Root = root, Body = body, Wings = wings, Born = m_T, Seed = Random.value * 100f };
            Vector3 start;
            if (from.HasValue || m_World == null) { start = from ?? m_Portal.Root.position; root.localScale = Vector3.one * 0.01f; }
            else
            {
                // it comes up the meadow's path from far away, then hops out through the opening (headset 2026-11-25:
                // "there needs to be more of a connection of what's happening in the middle")
                var w = m_World.Root;
                float fly = kind == DiscordKind.Squeak ? 0.35f : kind == DiscordKind.Wobbler ? 0.1f : 0.075f; // walkers' middles above the ground
                float sx = Random.Range(-0.7f, 0.7f), sz = Random.Range(2.4f, 3.1f);
                start = w.TransformPoint(new Vector3(sx, StoryDiorama.GroundY + StoryDiorama.GroundAt(sx, sz) + fly, sz));
                d.Inside = true;
                d.Start = start;
                float mx = Random.Range(-0.3f, 0.3f);
                d.Mid = w.TransformPoint(new Vector3(mx, StoryDiorama.GroundY + StoryDiorama.GroundAt(mx, 0.9f) + fly, 0.9f));
                d.Cross = w.TransformPoint(new Vector3(Random.Range(-0.18f, 0.18f), Random.Range(-0.2f, 0.05f), -0.02f));
                d.Back = StoryArt.ToPortal(root); // seen only through the opening while it is in the meadow
                root.localScale = Vector3.one;
            }
            root.position = start;
            // where it heads: somewhere round the player, never straight at the face
            d.Aim = new Vector3(Random.Range(-0.55f, 0.55f), Random.Range(-0.25f, 0.15f), 0f);
            // its badge: a little copy of the friend that beats it, floating above
            d.Badge = StoryArt.Friend(StoryRules.Beats(kind), 0.04f, root);
            d.Badge.localPosition = new Vector3(0f, kind == DiscordKind.Wobbler ? 0.17f : 0.13f, 0f);
            m_Discords.Add(d);
            m_Audio.Play(kind == DiscordKind.Squeak ? StorySound.SqueakCall : kind == DiscordKind.Wobbler ? StorySound.WobbleCall : StorySound.ClangCall, start, 0.8f);
        }

        float Speed(DiscordKind k) => (k == DiscordKind.Squeak ? 0.3f : k == DiscordKind.Wobbler ? 0.22f : 0.16f) * (Easy ? 0.6f : 1f) * (Level != null ? Level.Speed : 1f);

        void MoveDiscords(Vector3 head, float dt, float now)
        {
            foreach (var d in m_Discords)
            {
                float age = now - d.Born;
                float grow = d.Back != null ? 1f : Mathf.Clamp01(age / 0.7f);
                d.Root.localScale = Vector3.one * d.Size * Mathf.SmoothStep(0.05f, 1f, grow);
                var p = d.Root.position;
                float t = now + d.Seed;
                if (d.Inside)
                {
                    // up the path, then a hop to the opening
                    d.InsideFor += dt / (Easy ? 1.6f : 1f);
                    float f = Mathf.Clamp01(d.InsideFor / InsideSeconds);
                    p = f < 0.7f ? Vector3.Lerp(d.Start, d.Mid, f / 0.7f) : Vector3.Lerp(d.Mid, d.Cross, Mathf.SmoothStep(0f, 1f, (f - 0.7f) / 0.3f));
                    if (f < 0.7f && d.Kind != DiscordKind.Squeak) p.y += Mathf.Abs(Mathf.Sin(t * 7f)) * 0.025f;
                    if (f >= 1f) { d.Inside = false; StoryArt.Restore(d.Back); m_Audio.Play(d.Kind == DiscordKind.Squeak ? StorySound.SqueakCall : d.Kind == DiscordKind.Wobbler ? StorySound.WobbleCall : StorySound.ClangCall, p, 0.6f); }
                }
                else if (!d.Loose)
                {
                    var target = head + m_Right * d.Aim.x + Vector3.up * d.Aim.y;
                    var to = target - p;
                    var side = Vector3.Cross(Vector3.up, to.normalized);
                    var step = to.normalized * Speed(d.Kind) * dt;
                    if (d.Kind == DiscordKind.Squeak) step += (side * Mathf.Cos(t * 6f) * 0.5f + Vector3.up * Mathf.Sin(t * 4.3f) * 0.25f) * dt; // a zigzag
                    else if (d.Kind == DiscordKind.Wobbler) step += side * Mathf.Cos(t * 1.8f) * 0.3f * dt; // a wide sway
                    p += step;
                    if (Vector3.Distance(p, head) < EscapeDistance)
                    {
                        d.Loose = true;
                        m_GreyPulse = 0.35f;
                        m_Audio.Play(StorySound.Escape, p);
                        Banner(T("A Discord got into your room!"), 1.5f);
                        Emote(m_PalmFriend, RoomOrchestra.Expression.Scared);
                        Debug.Log($"[RoomOrchestra] Story: a {d.Kind} got into the room");
                    }
                }
                else
                {
                    // loose in the room: drifting round the player, greying the room until it is beaten
                    float a = t * 0.35f;
                    var target = head + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.15f + Vector3.up * (Mathf.Sin(a * 1.7f) * 0.2f - 0.05f);
                    p = Vector3.MoveTowards(p, target, 0.3f * (Easy ? 0.6f : 1f) * dt);
                }
                d.Root.position = p;
                d.Root.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(p - head, Vector3.up).normalized + Vector3.up * 0.001f);
                // each kind moves its own way
                switch (d.Kind)
                {
                    case DiscordKind.Squeak:
                        if (d.Wings != null) for (int s = 0; s < d.Wings.Length; s++) d.Wings[s].localRotation = Quaternion.Euler(0f, 0f, (s * 2 - 1) * Mathf.Sin(t * 40f) * 35f);
                        d.Body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 5f) * 15f);
                        break;
                    case DiscordKind.Wobbler:
                        float sq = Mathf.Sin(t * 6f) * 0.12f;
                        d.Body.localScale = new Vector3(1f - sq * 0.5f, 1f + sq, 1f - sq * 0.5f);
                        d.Body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.8f) * 12f);
                        break;
                    default:
                        d.Body.localPosition = Vector3.up * Mathf.Abs(Mathf.Sin(t * 3f)) * 0.03f; // heavy stomping
                        d.Body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 3f) * 5f);
                        break;
                }
                if (d.Badge != null) d.Badge.localRotation = Quaternion.Euler(0f, Mathf.Sin(t) * 25f, 0f); // its face toward the player
            }
        }

        // ---------- the Grumble ----------
        void SummonGrumble()
        {
            Boss = Level.NewBoss();
            m_Grumble = StoryArt.BuildGrumble(m_Root);
            m_Grumble.Root.position = m_Portal.Root.position;
            m_Grumble.Root.localScale = Vector3.one * 0.01f;
            m_PortalTarget = 1.45f;
            ShowShell();
            m_Audio.Play(StorySound.ClangCall, m_Grumble.Root.position, 1f);
            m_Audio.Play(StorySound.WobbleCall, m_Grumble.Root.position, 1f);
            Debug.Log("[RoomOrchestra] Story: the Grumble arrives");
        }

        void ShowShell()
        {
            if (m_Grumble == null) return;
            for (int i = 0; i < 3; i++) m_Grumble.Shells[i].SetActive(!Boss.Defeated && (DiscordKind)i == Boss.Shell); // spikes, jelly, armour
            if (m_Grumble.Badge != null) Destroy(m_Grumble.Badge.gameObject);
            if (!Boss.Defeated)
            {
                m_Grumble.Badge = StoryArt.Friend(StoryRules.Beats(Boss.Shell), 0.08f, m_Grumble.Root);
                m_Grumble.Badge.localPosition = new Vector3(0f, 0.45f, 0f);
            }
            BossCount();
        }

        /// <summary>Above the Grumble: which shell, and how many more hits it takes.</summary>
        void BossCount()
        {
            if (m_Grumble == null || Boss == null) return;
            if (m_BossText == null || m_BossText.transform.parent != m_Grumble.Root)
            {
                m_BossText = StoryArt.Words(m_Grumble.Root, "", new Vector3(0f, 0.62f, 0f), 0.055f, new Color(1f, 0.92f, 0.7f), 1f);
                m_BossText.textWrappingMode = TextWrappingModes.NoWrap;
            }
            var bars = new System.Text.StringBuilder();
            for (int i = 0; i < Boss.HitsPerShell; i++) bars.Append(i < Boss.HitsLeft ? "|" : ".");
            m_BossText.text = Boss.Defeated ? "" : F("Shell {0} of {1}: needs {2}  {3}", Boss.ShellIndex + 1, Boss.Shells.Count, FriendName(StoryRules.Beats(Boss.Shell)), bars);
        }

        void MoveGrumble(Vector3 head, float now)
        {
            if (m_Grumble == null) return;
            var r = m_Grumble.Root;
            float k = Mathf.MoveTowards(r.localScale.x, 0.72f, Time.deltaTime * 0.5f); // big, but the meadow still shows round it
            r.localScale = Vector3.one * k;
            // it hovers out in front of the portal, swaying and grumbling
            var home = m_Portal.Root.position - m_Forward * 0.45f + m_Right * Mathf.Sin(now * 0.6f) * 0.45f + Vector3.up * (Mathf.Sin(now * 1.1f) * 0.08f - 0.05f);
            r.position = Vector3.Lerp(r.position, home, Time.deltaTime * 1.5f);
            r.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(r.position - head, Vector3.up).normalized + Vector3.up * 0.001f);
            float shake = Mathf.Clamp01(1f - (now - m_GrumbleHitAt) / 0.4f);
            m_Grumble.Body.localPosition = Random.insideUnitSphere * 0.02f * shake;
            m_Grumble.Body.localScale = Vector3.one * (1f + Mathf.Sin(now * 2.2f) * 0.04f);
            if (m_Grumble.Badge != null) m_Grumble.Badge.localRotation = Quaternion.Euler(0f, Mathf.Sin(now) * 25f, 0f);
        }

        void HitGrumble(OrbType friend, Vector3 at, bool chorus)
        {
            if (Boss == null || Boss.Defeated) return;
            var result = chorus ? Boss.Chorus() : Boss.Hit(friend);
            m_GrumbleHitAt = m_T;
            switch (result)
            {
                case BossHit.Wrong:
                    Wrong++;
                    Harmony.Wrong();
                    m_Audio.Play(StorySound.Sour, at);
                    Banner(F("{0} bounces off! It needs {1}.", FriendName(friend), FriendName(StoryRules.Beats(Boss.Shell))), 2f);
                    break;
                case BossHit.Damaged:
                    BossCount();
                    m_Audio.Play(StorySound.ClangCall, at, 0.6f);
                    m_Audio.MelodyNote(friend);
                    Burst(at, friend, 6);
                    Banner(T("Crack!"), 1f);
                    break;
                case BossHit.ShellBroken:
                    m_Audio.Play(StorySound.Shell, at);
                    m_Audio.MelodyNote(friend);
                    Puff(at, 1f);
                    Burst(at, friend, 14);
                    ShowShell();
                    Banner(F("Shell broken! Now it needs {0}.", FriendName(StoryRules.Beats(Boss.Shell))), 2.5f);
                    break;
                case BossHit.Defeated:
                    m_Audio.Play(StorySound.Shell, at);
                    Puff(m_Grumble.Root.position, 2f);
                    Burst(m_Grumble.Root.position, OrbType.Flutter, 12); Burst(m_Grumble.Root.position, OrbType.Bop, 12); Burst(m_Grumble.Root.position, OrbType.Boom, 12);
                    Destroy(m_Grumble.Root.gameObject); m_Grumble = null;
                    foreach (var d in m_Discords.ToArray()) BeatDiscord(d, OrbType.Flutter, false, quiet: true);
                    Win();
                    break;
            }
            if (result != BossHit.Wrong) Harmony.Matched(false);
            Debug.Log($"[RoomOrchestra] Story: {friend} hit the Grumble: {result}");
        }

        // ---------- the friends flying ----------
        void Launch(Vector3 from, Vector3 direction, string how)
        {
            if (Current == Stage.Won || Current == Stage.Lost || Current == Stage.Intro) return; // nothing to flick at yet
            bool onBeat = m_Audio.OnBeat();
            if (Harmony.Spend()) { Chorus(from); return; }
            // the aim line's target first (the Discord this friend beats): a flick roughly its way goes to it
            // Easy aim on vs off, clearly different (owner, headset 2026-11-26: "make the difference a little bit more
            // drastic"): on, any flick roughly forward finds its target; off, the flick must be aimed.
            var lockOn = AimTarget(from);
            if (lockOn.HasValue && Vector3.Angle(direction, lockOn.Value - from) < (Easy ? 75f : 35f))
                direction = Vector3.Slerp(direction, (lockOn.Value - from).normalized, Easy ? 1f : 0.8f).normalized;
            else
            {
                var targets = new List<Vector3>();
                foreach (var d in m_Discords) targets.Add(d.Root.position);
                if (m_Grumble != null) targets.Add(m_Grumble.Root.position);
                direction = FlickDetector.Assist(from, direction, targets.ToArray(), Easy ? 60f : 20f, Easy ? 0.95f : 0.6f);
            }
            var friend = Queue.Flick();
            FlickBurst(from, friend);
            var s = NewShot(friend, from, direction * (friend == OrbType.Flutter ? 5.5f : friend == OrbType.Bop ? 4.2f : 3.3f), onBeat);
            m_Audio.Play(StorySound.Launch, from);
            if (onBeat) { Banner(T("On the beat!"), 0.8f); Burst(from, friend, 4); }
            Debug.Log($"[RoomOrchestra] Story: {friend} launched ({how}, finger {m_Flick.Speed:F1} m/s{(onBeat ? ", on the beat" : "")})");
            RefreshHand();
            if (!m_HoldTaught && WaveIndex >= 1 && Hands()) m_HoldTaught = true;
        }

        Shot NewShot(OrbType friend, Vector3 from, Vector3 velocity, bool onBeat)
        {
            var body = StoryArt.Friend(friend, 0.07f, m_Root);
            body.position = from;
            var trail = body.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.25f; trail.widthMultiplier = 0.03f; trail.minVertexDistance = 0.01f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var c = onBeat ? new Color(1f, 0.85f, 0.35f) : StoryArt.FriendColor(friend);
            trail.sharedMaterial = StoryArt.Glow(new Color(c.r, c.g, c.b, 0.7f), transparent: true, queueOffset: 6);
            trail.startColor = new Color(c.r, c.g, c.b, 0.8f); trail.endColor = new Color(c.r, c.g, c.b, 0f);
            var s = new Shot { Friend = friend, Body = body, Velocity = velocity, Born = m_T, Until = m_T + (friend == OrbType.Boom ? 1.5f : 1.8f), OnBeat = onBeat };
            m_Shots.Add(s);
            return s;
        }

        void Chorus(Vector3 from)
        {
            Queue.Flick();
            RefreshHand();
            m_ChorusShown = false;
            Banner("CHORUS!", 2f);
            m_ChorusBossDone = false;
            m_Audio.Play(StorySound.Chorus, from);
            var friends = new[] { OrbType.Flutter, OrbType.Bop, OrbType.Boom };
            var targets = new List<object>();
            foreach (var d in m_Discords) if (!d.Inside) targets.Add(d);
            if (m_Grumble != null) targets.Add(m_Grumble);
            int n = Mathf.Max(3, targets.Count);
            for (int i = 0; i < n && i < 10; i++)
            {
                var friend = friends[i % 3];
                var dir = Quaternion.AngleAxis((i % 3 - 1) * 20f, Vector3.up) * (m_Forward + Vector3.up * 0.2f);
                var s = NewShot(friend, from, dir.normalized * 4f, true);
                s.Chorus = true;
                s.Target = i < targets.Count ? targets[i] : null;
            }
            Debug.Log($"[RoomOrchestra] Story: Chorus at {targets.Count} targets");
        }

        void MoveShots(float dt, float now, Vector3 head)
        {
            for (int i = m_Shots.Count - 1; i >= 0; i--)
            {
                var s = m_Shots[i];
                var p = s.Body.position;
                // a Chorus friend homes in on its target
                if (s.Chorus && s.Target != null)
                {
                    var tp = s.Target is Discord td ? (td.Root != null ? td.Root.position : p) : m_Grumble != null ? m_Grumble.Root.position : p;
                    s.Velocity = Vector3.Slerp(s.Velocity, (tp - p).normalized * 5f, dt * 6f);
                }
                else if (s.Friend == OrbType.Boom) s.Velocity += Vector3.down * 1.0f * dt; // a heavy lob
                else if (s.Friend == OrbType.Bop && (s.Bounces > 0 || now - s.Born > 0.35f))
                {
                    // Bop's trick: after a bounce (or a moment in the air) it curves toward the nearest Wobbler
                    var aim = Nearest(p, DiscordKind.Wobbler, 1.6f);
                    if (aim.HasValue) s.Velocity = Vector3.Slerp(s.Velocity, (aim.Value - p).normalized * s.Velocity.magnitude, dt * 4f);
                }
                var step = s.Velocity * dt;
                // real walls: Bop ricochets, Boom bursts, the rest stop
                if (Physics.Raycast(p, step.normalized, out var wall, step.magnitude + 0.03f, 1 << RealWalls.SurfaceLayer, QueryTriggerInteraction.Ignore))
                {
                    if (s.Friend == OrbType.Bop && s.Bounces < 3 && !s.Chorus)
                    {
                        s.Velocity = Vector3.Reflect(s.Velocity, wall.normal);
                        s.Bounces++;
                        p = wall.point + wall.normal * 0.03f;
                        UiSounds.Play(UiSound.Press, p);
                        step = Vector3.zero;
                    }
                    else if (s.Friend == OrbType.Boom && !s.Chorus) { Shockwave(wall.point, s); Kill(i); continue; }
                    else { Puff(wall.point, 0.3f); Kill(i); continue; }
                }
                p += step;
                s.Body.position = p;
                s.Body.Rotate(0f, 540f * dt, 0f, Space.Self);
                bool spent = now > s.Until;
                if (spent && s.Friend == OrbType.Boom && !s.Chorus) { Shockwave(p, s); Kill(i); continue; }
                // the Grumble
                if (!spent && m_Grumble != null && !s.Hit.Contains(m_Grumble) && Vector3.Distance(p, m_Grumble.Root.position) < 0.3f * m_Grumble.Root.localScale.x + 0.04f)
                {
                    s.Hit.Add(m_Grumble);
                    s.HitAny = true;
                    if (s.Chorus) { if (!m_ChorusBossDone) { m_ChorusBossDone = true; HitGrumble(s.Friend, p, true); } else Burst(p, s.Friend, 6); }
                    else if (s.Friend == OrbType.Boom) Shockwave(p, s);
                    else HitGrumble(s.Friend, p, false);
                    spent = true;
                }
                // Discords
                for (int k = m_Discords.Count - 1; k >= 0 && !spent; k--)
                {
                    var d = m_Discords[k];
                    if (s.Hit.Contains(d)) continue;
                    if (Vector3.Distance(p, d.Root.position) > HitRadius * d.Size + (s.Friend == OrbType.Boom ? 0.04f : 0f)) continue;
                    s.Hit.Add(d);
                    s.HitAny = true;
                    if (s.Chorus) { BeatDiscord(d, s.Friend, true); spent = s.Target == d || s.Target == null; continue; }
                    if (s.Friend == OrbType.Boom) { Shockwave(p, s); spent = true; break; }
                    if (StoryRules.Matches(s.Friend, d.Kind))
                    {
                        BeatDiscord(d, s.Friend, s.OnBeat);
                        if (s.Friend != OrbType.Flutter) spent = true; // Flutter pierces on through the line
                    }
                    else { Sour(d, s.Friend); spent = true; }
                }
                if (spent || Vector3.Distance(p, head) > 8f) Kill(i);
            }
        }

        Vector3? Nearest(Vector3 from, DiscordKind kind, float within)
        {
            Vector3? best = null; float bestD = within;
            foreach (var d in m_Discords)
            {
                if (d.Kind != kind || d.Inside) continue;
                float dist = Vector3.Distance(from, d.Root.position);
                if (dist < bestD) { bestD = dist; best = d.Root.position; }
            }
            if (m_Grumble != null && Boss != null && !Boss.Defeated && Boss.Shell == kind)
            {
                float dist = Vector3.Distance(from, m_Grumble.Root.position);
                if (dist < bestD) best = m_Grumble.Root.position;
            }
            return best;
        }

        void Kill(int i)
        {
            var s = m_Shots[i];
            if (!s.HitAny && !s.Chorus) Missed(s.Friend);
            if (m_Shots[i].Body != null) Destroy(m_Shots[i].Body.gameObject);
            m_Shots.RemoveAt(i);
        }

        /// <summary>Boom lands: a heavy shockwave. Clangs in reach crack; the others are knocked back (and don't grow).</summary>
        void Shockwave(Vector3 at, Shot s)
        {
            const float Reach = 0.5f;
            m_Audio.Play(StorySound.Beat, at, 1f);
            var ring = StoryArt.Part(PrimitiveType.Sphere, m_Root, Vector3.zero, Vector3.one * 0.1f, StoryArt.Glow(new Color(1f, 0.65f, 0.3f, 0.45f), transparent: true, queueOffset: 30), "Shockwave");
            ring.transform.position = at;
            var m = ring.GetComponent<MeshRenderer>().sharedMaterial;
            m_Bits.Add(new Bit { T = ring.transform, Born = m_T, Life = 0.4f, M = m, C = m.color, Grow = Reach * 2f });
            bool cracked = false, knocked = false, grumble = false;
            foreach (var d in m_Discords.ToArray())
            {
                if (d.Inside) continue;
                float dist = Vector3.Distance(at, d.Root.position);
                if (dist > Reach * Mathf.Max(1f, d.Size)) continue;
                if (d.Kind == DiscordKind.Clang) { BeatDiscord(d, OrbType.Boom, s.OnBeat); cracked = true; }
                else { d.Root.position += (d.Root.position - at).normalized * 0.25f; knocked = true; } // knocked back
            }
            if (m_Grumble != null && Vector3.Distance(at, m_Grumble.Root.position) < Reach + 0.15f) { HitGrumble(OrbType.Boom, at, false); grumble = true; }
            if (cracked || knocked || grumble) s.HitAny = true;
            if (knocked && !cracked && !grumble)
            {
                // the wrong friend: a sour note, and the streak ends
                Wrong++;
                Harmony.Wrong();
                m_WatchFlashUntil = Time.time + 0.5f; m_StreakLostUntil = Time.time + 1.2f;
                m_Audio.Play(StorySound.Sour, at);
                Banner(T("Sour note! Boom's shockwave only cracks Clangs."), 1.8f);
            }
            if (!cracked && m_Grumble == null) Debug.Log("[RoomOrchestra] Story: Boom's shockwave cracked no Clang");
        }

        /// <summary>A beaten Discord's song: a glowing note flies back into the portal and brings colour back to the meadow.</summary>
        void SendNote(Vector3 from, OrbType friend)
        {
            if (m_Root == null || m_Portal == null) return;
            var c = Color.Lerp(StoryArt.FriendColor(friend), new Color(1f, 0.95f, 0.7f), 0.4f);
            var go = StoryArt.Part(PrimitiveType.Sphere, m_Root, Vector3.zero, Vector3.one * 0.03f, StoryArt.Glow(c), "SongNote");
            go.transform.position = from;
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.3f; trail.widthMultiplier = 0.02f; trail.minVertexDistance = 0.01f;
            trail.sharedMaterial = StoryArt.Glow(new Color(c.r, c.g, c.b, 0.6f), transparent: true, queueOffset: 7);
            trail.startColor = new Color(c.r, c.g, c.b, 0.7f); trail.endColor = new Color(c.r, c.g, c.b, 0f);
            var to = m_Portal.Root.position + m_Forward * 0.05f + Random.insideUnitSphere * 0.12f;
            m_Notes.Add(new Note { T = go.transform, From = from, To = to, Born = m_T });
        }

        void MoveNotes(float now)
        {
            for (int i = m_Notes.Count - 1; i >= 0; i--)
            {
                var n = m_Notes[i];
                float f = (now - n.Born) / NoteSeconds;
                if (n.T == null) { m_Notes.RemoveAt(i); continue; }
                if (f >= 1f)
                {
                    // it reaches the meadow: colour comes back, the ring flashes, the way opens a little wider
                    Destroy(n.T.gameObject); m_Notes.RemoveAt(i);
                    m_Restored++;
                    m_BloomTarget = Mathf.Max(m_BloomTarget, 0.12f + 0.78f * Mathf.Clamp01(m_Restored / (float)Mathf.Max(1, Level.Total)));
                    m_FlashRingUntil = Time.time + 0.25f;
                    continue;
                }
                var mid = (n.From + n.To) * 0.5f + Vector3.up * 0.25f;
                float e = Mathf.SmoothStep(0f, 1f, f);
                n.T.position = Vector3.Lerp(Vector3.Lerp(n.From, mid, e), Vector3.Lerp(mid, n.To, e), e);
                n.T.localScale = Vector3.one * Mathf.Lerp(0.03f, 0.015f, f);
            }
        }

        /// <summary>A friend flew and hit nothing: the streak ends (headset 2026-11-25).</summary>
        void Missed(OrbType friend)
        {
            if (Harmony == null) return;
            if (Harmony.Streak >= 2) { m_StreakLostUntil = Time.time + 1.2f; m_WatchFlashUntil = Time.time + 0.4f; }
            Harmony.Missed(Level != null ? Level.MissCost : 0f);
            Debug.Log($"[RoomOrchestra] Story: {friend} hit nothing; streak over");
        }

        void BeatDiscord(Discord d, OrbType friend, bool onBeat, bool quiet = false)
        {
            if (!m_Discords.Contains(d)) return;
            m_Discords.Remove(d);
            var at = d.Root.position;
            if (!quiet) { m_Audio.MelodyNote(friend); Puff(at, 0.45f); }
            Burst(at, friend, 8);
            SendNote(at, friend);
            if (d.Loose) Meter.Beaten();
            Harmony.Matched(onBeat);
            Beaten++;
            if (m_PalmFriend != null && m_PalmFriend.TryGetComponent<CharacterFace>(out var face)) face.Beam();
            if (Harmony.Streak >= 3 && Harmony.Streak != m_LastStreakShown && Harmony.Streak % 3 == 0) { m_LastStreakShown = Harmony.Streak; Banner(F("{0} in a row!", Harmony.Streak), 1.2f); }
            Destroy(d.Root.gameObject);
            Debug.Log($"[RoomOrchestra] Story: {friend} beat a {d.Kind}{(onBeat ? " on the beat" : "")}");
        }

        void Sour(Discord d, OrbType friend)
        {
            Wrong++;
            Harmony.Wrong();
            m_WatchFlashUntil = Time.time + 0.5f; m_StreakLostUntil = Time.time + 1.2f;
            m_Audio.Play(StorySound.Sour, d.Root.position);
            if (d.Size < 1.6f) d.Size *= 1.15f; // the wrong friend: it grows
            Banner(F("Sour note! {0} beats that one.", FriendName(StoryRules.Beats(d.Kind))), 1.8f);
            Debug.Log($"[RoomOrchestra] Story: {friend} can't beat a {d.Kind}: it grows");
        }

        /// <summary>A soft white puff (story mode's own: the sandbox's poof is on a layer the camera skips in story).</summary>
        void Puff(Vector3 at, float size = 1f)
        {
            if (m_Root == null) return;
            var go = StoryArt.Part(PrimitiveType.Sphere, m_Root, Vector3.zero, Vector3.one * 0.05f * size, StoryArt.Glow(new Color(1f, 0.98f, 0.94f, 0.55f), transparent: true, queueOffset: 31), "Puff");
            go.transform.position = at;
            var m = go.GetComponent<MeshRenderer>().sharedMaterial;
            m_Bits.Add(new Bit { T = go.transform, Born = m_T, Life = 0.45f, M = m, C = m.color, Grow = 0.22f * size });
        }

        /// <summary>A beaten Discord turns back into music: little bits in the friends' colours scatter and fade.</summary>
        void Burst(Vector3 at, OrbType friend, int count)
        {
            var colors = new[] { StoryArt.FriendColor(friend), new Color(1f, 0.92f, 0.6f), Color.white };
            for (int i = 0; i < count; i++)
            {
                var c = colors[i % colors.Length];
                var go = StoryArt.Part(i % 2 == 0 ? PrimitiveType.Sphere : PrimitiveType.Cube, m_Root, Vector3.zero, Vector3.one * 0.018f, StoryArt.Glow(c, transparent: true, queueOffset: 8), "Bit");
                go.transform.position = at;
                go.transform.rotation = Random.rotation;
                var m = go.GetComponent<MeshRenderer>().sharedMaterial;
                m_Bits.Add(new Bit { T = go.transform, V = Random.onUnitSphere * Random.Range(0.4f, 0.9f) + Vector3.up * 0.3f, Born = m_T, Life = Random.Range(0.5f, 0.9f), M = m, C = c });
            }
        }

        void MoveBits(float now, float dt)
        {
            for (int i = m_Bits.Count - 1; i >= 0; i--)
            {
                var b = m_Bits[i];
                float age = (now - b.Born) / b.Life;
                if (age >= 1f || b.T == null) { if (b.T != null) Destroy(b.T.gameObject); m_Bits.RemoveAt(i); continue; }
                if (b.Grow > 0f) b.T.localScale = Vector3.one * Mathf.Lerp(0.1f, b.Grow, age);
                else { b.T.position += b.V * dt; b.V += Vector3.down * 0.6f * dt; b.T.Rotate(200f * dt, 150f * dt, 0f); }
                b.M.color = new Color(b.C.r, b.C.g, b.C.b, b.C.a * (1f - age));
            }
        }

        // ---------- the end ----------
        void Win()
        {
            SetStage(Stage.Won);
            Stars = StoryScore.Stars(true, MeterPeak, Wrong);
            foreach (var d in m_Discords.ToArray()) BeatDiscord(d, OrbType.Bop, false, quiet: true);
            if (m_Grumble != null) { Destroy(m_Grumble.Root.gameObject); m_Grumble = null; }
            m_PortalTarget = 1.5f;
            m_BloomTarget = 1f; // the meadow's song is back: full colour, the World Tree blazing
            m_Audio.Play(StorySound.Win, m_Portal.Root.position);
            Meter = new DiscordMeter(MeterAssist); // the room clears
            m_Banner.text = F("Level {0} complete!", Level.Number);
            bool more = m_LevelIndex + 1 < StoryLevel.All.Length;
            Say(F("{0} The Grumble and {1} Discords turned back into music.", T(Lore.Restored), Beaten) + " " + (more ? F("Level {0} is ready: press Next level when you've collected the card.", m_LevelIndex + 2) : T("You restored every instrument: the journey is complete! Thank you for playing.")));
            ShowCollectCard();
            m_EndStars = new GameObject("Stars");
            m_EndStars.transform.SetParent(m_Hud, false);
            for (int i = 0; i < 3; i++) StoryArt.Star(m_EndStars.transform, new Vector3((i - 1) * 0.22f, 1.22f, -0.08f), 0.16f, i < Stars); // above the title, clear of the banner
            for (int k = 0; k < 3; k++) Burst(m_Portal.Root.position - m_Forward * 0.1f, (OrbType)k, 10);
            Labels();
            Debug.Log($"[RoomOrchestra] Story: Level {Level.Number} won, {Stars} stars (beaten {Beaten}, wrong {Wrong}, Discord peak {MeterPeak:F2})");
        }

        void Lose()
        {
            SetStage(Stage.Lost);
            m_BloomTarget = 0.05f; // the colour drains away
            m_Audio.Stop();
            m_Audio.Play(StorySound.Lose, Head().position);
            m_Banner.text = T("Oh no!");
            Say(T(Easy ? "Too many Discords got into your room, and the meadow went grey. Try again?" : "Too many Discords got into your room, and the meadow went grey. Try again? Easy aim slows the Discords down and helps your aim."));
            Labels();
            Debug.Log($"[RoomOrchestra] Story: Level {Level.Number} lost at wave {WaveIndex + 1} (beaten {Beaten}, wrong {Wrong})");
        }

        static Transform s_NoHead;
        static Transform Head()
        {
            if (Camera.main != null) return Camera.main.transform;
            if (s_NoHead == null) s_NoHead = new GameObject("StoryNoHead").transform;
            return s_NoHead;
        }
    }

    /// <summary>Menu > Story (owner, 2026-11-26: "place an option there to play the story mode" on the menu's first row;
    /// the floating Play Story sign is gone): closes the menu and opens story mode.</summary>
    public class StoryMenuButton : MonoBehaviour, IPressable
    {
        public void Press()
        {
            MenuTray.Instance?.SetOpen(false);
            StoryDirector.Instance?.Begin();
        }
    }

    /// <summary>A button in story mode (pressed by pointing: eyes and pinch, or the controller's ray and trigger).</summary>
    public class StoryButton : MonoBehaviour
    {
        System.Action m_OnPress;
        TMP_Text m_Label;
        Material m_Mat;
        Color m_Base;
        bool m_Hover;

        public static StoryButton Create(Transform parent, Vector3 at, Color face, Color ink, System.Action onPress, string name = "StoryButton")
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = StoryArt.Layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = new Vector3(0.32f, 0.1f, 0.03f);
            var b = go.AddComponent<StoryButton>();
            b.m_Mat = StoryArt.Lit(face, 0.5f);
            b.m_Base = face;
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = b.m_Mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            b.m_OnPress = onPress;
            var label = new GameObject("Label").transform;
            label.SetParent(parent, false);
            label.localPosition = at + new Vector3(0f, 0f, -0.017f);
            b.m_Label = StoryArt.Words(label, "", Vector3.zero, 0.045f, ink, 0.32f);
            return b;
        }

        public void SetLabel(string text) => m_Label.text = text;

        public void SetHover(bool on)
        {
            if (on == m_Hover) return;
            m_Hover = on;
            m_Mat.color = on ? Color.Lerp(m_Base, new Color(1f, 0.85f, 0.4f), 0.5f) : m_Base;
            if (m_Mat.HasProperty("_BaseColor")) m_Mat.SetColor("_BaseColor", m_Mat.color);
            if (on) UiSounds.Play(UiSound.Hover, transform.position);
        }

        public void Press() => m_OnPress?.Invoke();

        /// <summary>The story button a ray points at, if any.</summary>
        public static StoryButton Under(Vector3 origin, Vector3 direction, out Vector3 point)
        {
            point = default;
            StoryButton best = null; float bestD = float.MaxValue;
            foreach (var hit in Physics.RaycastAll(origin, direction, 8f, ~0, QueryTriggerInteraction.Collide))
            {
                var b = hit.collider.GetComponent<StoryButton>();
                if (b != null && hit.distance < bestD) { bestD = hit.distance; best = b; point = hit.point - direction * 0.01f; }
            }
            return best;
        }
    }
}
