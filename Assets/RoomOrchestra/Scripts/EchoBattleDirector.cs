using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// The Echo Battle (easter egg, a Version 3 nice-to-have; owner 2026-10-30): two headsets on the same Wi-Fi take turns.
    /// One calls a phrase of up to 4 notes in one bar; the other hears it played on their own furniture and echoes it in the
    /// next bar; exact notes score 2, one 16th off 1 (<see cref="EchoBattleRules"/>). Unlock: pick Flutter, Bop, Boom,
    /// Flutter, Bop, Boom within 6 s (<see cref="SecretUnlock"/>); the same again hides it. A card beside the leaf has Host,
    /// Join and Leave (Leave also puts the card away), a status line (with a 3-2-1-GO count) and the score. Each headset judges its own player against its
    /// own beat (the music clock), so Wi-Fi delay never decides a point; only notes and scores travel (<see cref="EchoBattleLink"/>).
    /// During a battle notes are one-shots: they sound and are taken straight back out of the song, so nothing loops. The
    /// battle is a separate mode that never takes over normal play (owner): the player's own song is set aside (kept,
    /// silent) while a battle runs and plays on as before when they leave it.
    /// Taps, the controller and thrown balls all play notes, as everywhere else.
    /// Since 2026-11-22 (owner: "an easier way to access than the easter egg") Menu > Play together brings the card
    /// out too. With nobody found after <see cref="StillLookingSeconds"/> the card says what to check.
    /// </summary>
    public class EchoBattleDirector : MonoBehaviour
    {
        public const float ParkedScale = 0.12f, ResultSeconds = 2.5f; // 12%: the street card still fits the cloud, and its thinnest drip stays above 1 mm
        /// <summary>After this long without the other headset, the card says what to check.</summary>
        public const float StillLookingSeconds = 20f;

        public enum Phase { Hidden, Menu, Pairing, CountIn, Recording, WaitingForThem, Listening, Between, Over, Setup }

        [SerializeField] Transform card;
        [SerializeField] Vector3 cardHome, cardScale = Vector3.one;
        [SerializeField] Transform park;
        [SerializeField] TMP_Text status, score;
        [SerializeField] EchoConductor conductor;
        [Tooltip("The street-style title tag, English then Chinese; the one not in use waits behind the card's wall.")]
        [SerializeField] Transform[] titles;
        [SerializeField] Vector3 titleShown;

        readonly SecretUnlock m_Unlock = new();
        readonly List<PhraseNote> m_Recorded = new();
        OrbType m_LastOrb;
        bool m_HaveOrb;
        EchoBattleLink m_Link;
        EchoBattleMatch m_Match;
        long m_CountBar = -1, m_ListenBar = -1;
        bool m_Echoing;
        float m_NextTurnAt = -1f;
        int m_LastBeatShown = -1;
        AndroidMulticast m_Multicast;
        // Test hooks: two ends on one machine use their own ports and loopback (EchoBattleLink).
        int m_LocalPort = EchoBattleLink.DefaultPort, m_PeerPort = EchoBattleLink.DefaultPort;
        System.Net.IPAddress m_Broadcast;
        // The host's instruments, chosen and locked in when HOST is pressed; the joining player puts the same ones on
        // their furniture, and only then does play start (owner, 2026-11-16).
        List<InstrumentType> m_Set = new();
        InstrumentType[] m_OwnPlaces; // the joining player's own arrangement, put back when the battle ends
        bool m_Hosting, m_ReadySent;
        float m_SayAgainAt = -1f;
        float m_PairingSince;
        bool m_StillLookingSaid;

        /// <summary>The instruments this battle is played on (the host's).</summary>
        public IReadOnlyList<InstrumentType> Instruments => m_Set;

        public static EchoBattleDirector Instance { get; private set; }
        public Phase State { get; private set; } = Phase.Hidden;
        public EchoBattleMatch Match => m_Match;
        public string StatusText => status != null ? status.text : "";
        public bool Shown => State != Phase.Hidden;
        public Transform Card => card;

        public void Configure(Transform battleCard, Transform parkPoint, TMP_Text statusText, TMP_Text scoreText, EchoConductor echoes)
        {
            card = battleCard;
            park = parkPoint;
            status = statusText;
            score = scoreText;
            conductor = echoes;
            cardHome = card.localPosition;
            cardScale = card.localScale;
        }

        public void ConfigureTitles(Transform[] titleWords)
        {
            titles = titleWords;
            if (titles.Length > 0) titleShown = titles[0].localPosition;
            ShowTitle(UiLanguage.English);
        }

        /// <summary>The title tag in the language in use (the other one sits behind the card's brick wall).</summary>
        public Transform ShownTitle { get; private set; }

        void ShowTitle(UiLanguage language)
        {
            if (titles == null || titles.Length == 0) return;
            int want = language == UiLanguage.Chinese && titles.Length > 1 ? 1 : 0;
            for (int i = 0; i < titles.Length; i++)
                titles[i].localPosition = i == want ? titleShown : titleShown + new Vector3(0f, 0f, 0.12f); // behind the wall
            ShownTitle = titles[want];
        }

        public void UseTestNetwork(int localPort, int peerPort)
        {
            m_LocalPort = localPort;
            m_PeerPort = peerPort;
            m_Broadcast = System.Net.IPAddress.Loopback;
        }

        void OnEnable()
        {
            Instance = this;
            Localization.Changed += ShowTitle;
            EchoConductor.NoteAdded += OnNoteAdded;
            MusicalHitPipeline.Committed += OnHit;
            InstrumentPacks.RefusedWhileLocked += OnLockedPress;
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            Localization.Changed -= ShowTitle;
            EchoConductor.NoteAdded -= OnNoteAdded;
            MusicalHitPipeline.Committed -= OnHit;
            InstrumentPacks.RefusedWhileLocked -= OnLockedPress;
            CloseLink();
        }

        bool InBattle => State != Phase.Hidden && State != Phase.Menu && State != Phase.Over;

        // HOST and JOIN show only where they do something (the card's menu, and after a battle); while pairing or
        // playing, LEAVE is the only button, so there is nothing to press by mistake.
        Phase m_ButtonsFor = (Phase)(-1);

        void ShowButtonsFor(Phase state)
        {
            if (state == m_ButtonsFor || card == null) return;
            m_ButtonsFor = state;
            bool choosing = state == Phase.Menu || state == Phase.Over;
            foreach (var button in card.GetComponentsInChildren<EchoBattleButton>(true))
                if (button.ButtonKind != EchoBattleButton.Kind.Leave && button.gameObject.activeSelf != choosing)
                    button.gameObject.SetActive(choosing);
        }

        void Update()
        {
            WatchForTheSecret();
            ShowButtonsFor(State);
            if (State == Phase.Hidden)
            {
                if (park != null && card != null && card.position != park.position) Park();
                return;
            }
            if (m_Link != null)
            {
                foreach (var message in m_Link.Poll(Time.time)) Handle(message);
                if (m_Link != null && m_Link.Status.StartsWith("The other headset")) Lost();
            }
            Tick();
        }

        // ---- The easter egg ----
        void WatchForTheSecret()
        {
            var selector = OrbSelector.Instance;
            if (selector == null) return;
            var orb = selector.SelectedOrb;
            if (m_HaveOrb && orb == m_LastOrb) return;
            bool first = !m_HaveOrb;
            m_HaveOrb = true;
            m_LastOrb = orb;
            if (first || !m_Unlock.Selected(orb, Time.time)) return;
            // The secret only ever brings the card out (repeating it must not put away what it just showed); Leave closes it.
            if (State != Phase.Hidden) return;
            if (TutorialDirector.Instance != null && TutorialDirector.Instance.Active)
            {
                Debug.Log("[RoomOrchestra] Echo Battle: found, but the lesson is on (finish or skip it first)");
                return;
            }
            Show();
        }

        /// <summary>Menu, Play together: the card, saying how to play (two headsets on one Wi-Fi). Already out: it stays as it is.</summary>
        public void ShowFromMenu()
        {
            TutorialDirector.Instance?.Dismiss(); // Flutter's bubble never covers the card (owner, 2026-11-22)
            if (State != Phase.Hidden)
            {
                if (card != null) PoofCloud.PoofAt(card.position, 2.5f); // here it is
                return;
            }
            Show();
            Say(UiTerm.BattleIntro);
            Debug.Log("[RoomOrchestra] Echo Battle: opened from the menu");
        }

        public void Show()
        {
            ShowTitle(Localization.Current);
            State = Phase.Menu;
            if (card != null)
            {
                card.localPosition = cardHome;
                card.localScale = cardScale;
                foreach (var c in card.GetComponentsInChildren<Collider>(true)) c.enabled = true;
            }
            Say(UiTerm.BattleFound);
            SetScore("");
            if (card != null) PoofCloud.PoofAt(card.position, 2.5f); // it arrives in a poof, so it's noticed
            Debug.Log("[RoomOrchestra] Echo Battle: unlocked (the secret: Flutter, Bop, Boom, twice)");
        }

        public void Hide()
        {
            Leave(sendBye: true);
            State = Phase.Hidden;
            Park();
            Debug.Log("[RoomOrchestra] Echo Battle: hidden");
        }

        void Park()
        {
            if (card == null || park == null) return;
            card.position = park.position;
            card.localScale = cardScale * ParkedScale;
            foreach (var c in card.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        }

        // ---- Buttons ----
        public void Press(EchoBattleButton.Kind kind)
        {
            switch (kind)
            {
                // HOST and JOIN only start something from the card's menu. Pressed again while pairing or in a battle they
                // do nothing (V4 feedback: "my coop partner ended up leaving the game by accidentally pressing join twice":
                // a second press reopened the link, which said goodbye to the other headset). Only LEAVE leaves.
                case EchoBattleButton.Kind.Host:
                    if (State == Phase.Menu || State == Phase.Over) Open(EchoBattleLink.Role.Host);
                    break;
                case EchoBattleButton.Kind.Join:
                    if (State == Phase.Menu || State == Phase.Over) Open(EchoBattleLink.Role.Guest);
                    break;
                case EchoBattleButton.Kind.Leave:
                    if (State == Phase.Menu || State == Phase.Over)
                    {
                        Hide(); // not in a battle: put the card away
                        break;
                    }
                    Leave(sendBye: true);
                    State = Phase.Menu;
                    Say(UiTerm.BattleFound);
                    SetScore("");
                    break;
            }
        }

        void Open(EchoBattleLink.Role role)
        {
            Leave(sendBye: true);
            conductor?.SetSongMuted(true); // the player's own song waits, kept and silent
            m_Multicast = AndroidMulticast.Acquire();
            m_Link = new EchoBattleLink(role, m_LocalPort, m_PeerPort, m_Broadcast);
            try { m_Link.Open(); }
            catch (System.Net.Sockets.SocketException e)
            {
                Debug.LogWarning($"[RoomOrchestra] Echo Battle: couldn't open the network ({e.SocketErrorCode})");
                CloseLink();
                conductor?.SetSongMuted(false);
                Say(UiTerm.BattleGone);
                return;
            }
            State = Phase.Pairing;
            m_PairingSince = Time.time;
            m_StillLookingSaid = false;
            m_Hosting = role == EchoBattleLink.Role.Host;
            m_ReadySent = false;
            m_Set = new List<InstrumentType>();
            if (m_Hosting)
            {
                // the host's instruments, as they are on the furniture now, are the battle's: locked in from here
                // left to right as the furniture stands (table, chair, lamp), as the card lists them
                var places = InstrumentPacks.Instance != null ? InstrumentPacks.Instance.Places() : null;
                if (places != null) foreach (var type in places) if (!m_Set.Contains(type)) m_Set.Add(type);
                foreach (var target in InstrumentTarget.Active) if (!m_Set.Contains(target.Instrument)) m_Set.Add(target.Instrument);
                InstrumentPacks.Locked = true;
                Say(UiTerm.BattleWaiting, SetNames());
                SetScore(string.Format(UiText.Get(UiTerm.BattleHostSet, Localization.Current), SetNames()));
            }
            else Say(UiTerm.BattleLooking);
            Debug.Log($"[RoomOrchestra] Echo Battle: {(role == EchoBattleLink.Role.Host ? "hosting" : "looking for a host")}");
        }

        void Leave(bool sendBye)
        {
            if (m_Link != null && sendBye && m_Link.Connected)
            {
                m_Link.Send("BYE");
                m_Link.Poll(Time.time); // one try; the other side also notices silence
            }
            CloseLink();
            m_Match = null;
            m_CountBar = m_ListenBar = -1;
            m_NextTurnAt = -1f;
            ReleaseInstruments();
            conductor?.SetSongMuted(false); // back to the player's own song
        }

        // The battle no longer holds the instruments; the joining player's own arrangement goes back on their furniture.
        void ReleaseInstruments()
        {
            InstrumentPacks.Locked = false;
            InstrumentPacks.Wanted = null;
            if (m_OwnPlaces != null) InstrumentPacks.Instance?.SetPlaces(m_OwnPlaces, remember: false); // the saved ones were never replaced
            m_OwnPlaces = null;
            m_ReadySent = false;
        }

        string SetNames() => UiText.Names(m_Set, Localization.Current);

        // Change pressed while the instruments are locked in: the card says why, then goes back to what it was saying.
        string m_SaidBefore, m_LastNeed;

        void OnLockedPress()
        {
            if (!InBattle || status == null) return;
            if (m_SayAgainAt < 0f) m_SaidBefore = status.text;
            Say(UiTerm.BattleLocked);
            m_SayAgainAt = Time.time + 2.5f;
        }

        // The joining player's room against the host's set: what is still missing, and READY once nothing is.
        void MatchTheHost()
        {
            if (m_Hosting || m_ReadySent) return;
            var here = new HashSet<InstrumentType>();
            foreach (var target in InstrumentTarget.Active) here.Add(target.Instrument);
            var missing = EchoBattleRules.Missing(m_Set, here, InstrumentTarget.Active.Count);
            SetScore(string.Format(UiText.Get(UiTerm.BattleHostSet, Localization.Current), SetNames()));
            if (missing.Count > 0)
            {
                string need = UiText.Names(missing, Localization.Current);
                if (need != m_LastNeed) Say(UiTerm.BattleNeed, need);
                m_LastNeed = need;
                return;
            }
            InstrumentPacks.Wanted = null;
            InstrumentPacks.Locked = true;
            m_ReadySent = true;
            m_Link?.Send("READY");
            Say(UiTerm.BattlePlaced);
            Debug.Log("[RoomOrchestra] Echo Battle: this room has the host's instruments; ready");
        }

        void CloseLink()
        {
            m_Link?.Dispose();
            m_Link = null;
            m_Multicast?.Release();
            m_Multicast = null;
        }

        void Lost()
        {
            Leave(sendBye: false);
            State = Phase.Menu;
            Say(UiTerm.BattleGone);
            Debug.Log("[RoomOrchestra] Echo Battle: the other headset stopped answering");
        }

        // ---- Messages ----
        void Handle(EchoBattleLink.Message message)
        {
            Debug.Log($"[RoomOrchestra] Echo Battle: got {message}");
            switch (message.Type)
            {
                case "JOIN": // we host: the guest is in, and is told which instruments this battle is played on
                    if (!m_Hosting || State != Phase.Pairing) return;
                    m_Link.Send("SET", EchoBattleRules.EncodeSet(m_Set));
                    State = Phase.Setup;
                    Say(UiTerm.BattleTheyPlace, SetNames());
                    break;
                case "SET": // we joined: put the host's instruments on this room's furniture
                    if (m_Hosting || State != Phase.Pairing || !EchoBattleRules.TryDecodeSet(message.Payload, out var set)) return;
                    m_Set = set;
                    m_OwnPlaces = InstrumentPacks.Instance != null ? InstrumentPacks.Instance.Places() : null;
                    // automatic (owner, 2026-11-22): the host's instruments go straight onto this room's furniture, not
                    // remembered, so the player's own come back after the battle; Change still works if one didn't fit
                    if (m_OwnPlaces != null) InstrumentPacks.Instance.SetPlaces(EchoBattleRules.MatchPlaces(m_Set, m_OwnPlaces), remember: false);
                    InstrumentPacks.Wanted = m_Set;
                    m_LastNeed = null;
                    State = Phase.Setup;
                    Debug.Log($"[RoomOrchestra] Echo Battle: the host plays {string.Join(", ", m_Set)}");
                    MatchTheHost();
                    break;
                case "READY": // we host: their room matches; we call first
                    if (!m_Hosting || State != Phase.Setup) return;
                    m_Link.Send("START");
                    StartMatch(isHost: true);
                    break;
                case "START":
                    if (m_Hosting || !m_ReadySent || m_Match != null) return;
                    StartMatch(isHost: false);
                    break;
                case "CALL":
                    var parts = message.Payload.Split('|');
                    if (m_Match == null || parts.Length < 2 || !EchoBattleRules.TryDecode(parts[1], out var phrase)) return;
                    m_Match.Heard(phrase);
                    Listen(phrase);
                    break;
                case "SCORE":
                    var s = message.Payload.Split('|');
                    if (m_Match == null || s.Length < 2 || !int.TryParse(s[1], out int points)) return;
                    m_Match.TheyScored(points);
                    Say(UiTerm.BattleGotPoints, points);
                    ShowScore();
                    NextTurnSoon();
                    break;
                case "BYE":
                    Lost();
                    break;
            }
        }

        void StartMatch(bool isHost)
        {
            m_Match = new EchoBattleMatch(isHost);
            Debug.Log($"[RoomOrchestra] Echo Battle: match on, {(isHost ? "we" : "they")} call first");
            BeginTurn();
        }

        // ---- Turns, on the music clock ----
        long Bar => conductor != null && conductor.Clock != null ? conductor.Clock.BarIndex(AudioSettings.dspTime) : 0;

        void BeginTurn()
        {
            m_NextTurnAt = -1f;
            ShowScore();
            if (m_Match.Over)
            {
                State = Phase.Over;
                Say(m_Match.Result > 0 ? UiTerm.BattleWin : m_Match.Result < 0 ? UiTerm.BattleLose : UiTerm.BattleDraw);
                Debug.Log($"[RoomOrchestra] Echo Battle: over, {m_Match.MyScore} to {m_Match.TheirScore}");
                CloseLink();
                ReleaseInstruments();
                conductor?.SetSongMuted(false);
                return;
            }
            if (m_Match.ICall)
            {
                m_Echoing = false;
                CountIn(Bar + 1);
            }
            else
            {
                State = Phase.WaitingForThem;
                Say(UiTerm.BattleTheyCall);
            }
        }

        void CountIn(long countBar)
        {
            m_CountBar = countBar;
            m_LastBeatShown = -1;
            State = Phase.CountIn;
            Say(UiTerm.BattleReady);
        }

        // The other player's call, played on this headset's own furniture in the next bar; then our count-in and echo.
        void Listen(IReadOnlyList<PhraseNote> phrase)
        {
            State = Phase.Listening;
            Say(phrase.Count > 0 ? UiTerm.BattleListen : UiTerm.BattleNothing, phrase.Count);
            m_ListenBar = Bar + 1;
            if (conductor == null || conductor.Clock == null) return;
            int played = 0;
            foreach (var note in phrase)
            {
                // on the same instrument here; if this room doesn't have it (the players chose different ones), on this
                // room's furniture in the caller's instrument's own sound: every called note is always heard
                var target = InstrumentTarget.Find(note.Instrument);
                AudioClip[] sound = null;
                if (target == null)
                {
                    target = InstrumentTarget.Active.Count > 0 ? InstrumentTarget.Active[played % InstrumentTarget.Active.Count] : null;
                    sound = InstrumentPacks.Instance != null && InstrumentPacks.Instance.Current != null ? InstrumentPacks.Instance.Current.For(note.Instrument) : null;
                }
                if (target == null) continue;
                target.PlayHeardAt(note.Note, conductor.Clock.TimeOf(m_ListenBar, note.Step), 1f, sound);
                played++;
            }
            Debug.Log($"[RoomOrchestra] Echo Battle: their call plays here next bar ({played} of {phrase.Count} notes)");
        }

        void NextTurnSoon()
        {
            State = Phase.Between;
            m_NextTurnAt = Time.time + ResultSeconds;
        }

        void Tick()
        {
            long bar = Bar;
            if (m_SayAgainAt >= 0f && Time.time >= m_SayAgainAt)
            {
                m_SayAgainAt = -1f;
                if (status != null && m_SaidBefore != null && status.text == UiText.Get(UiTerm.BattleLocked, Localization.Current)) status.text = m_SaidBefore;
            }
            switch (State)
            {
                case Phase.Pairing: // nobody yet: say what to check (once)
                    if (!m_StillLookingSaid && Time.time - m_PairingSince >= StillLookingSeconds)
                    {
                        m_StillLookingSaid = true;
                        Say(UiTerm.BattleStillLooking);
                    }
                    break;
                case Phase.Setup:
                    MatchTheHost();
                    break;
                case Phase.Listening:
                    if (bar >= m_ListenBar + 1)
                    {
                        m_Match.StartEcho();
                        m_Echoing = true;
                        CountIn(bar);
                    }
                    break;
                case Phase.CountIn:
                    if (bar < m_CountBar) break;
                    if (bar >= m_CountBar + 1)
                    {
                        State = Phase.Recording;
                        m_Recorded.Clear();
                        Say(m_Echoing ? UiTerm.BattleEcho : UiTerm.BattleYourCall);
                        break;
                    }
                    int beat = TutorialPlan.CountdownBeat(conductor.Clock.BarPhase(AudioSettings.dspTime));
                    if (beat != m_LastBeatShown && beat > 0)
                    {
                        m_LastBeatShown = beat;
                        if (status != null) status.text = (4 - beat).ToString(); // 3, 2, 1 on beats 2-4; GO on the next downbeat
                    }
                    break;
                case Phase.Recording:
                    if (bar >= m_CountBar + 2) EndRecording();
                    break;
                case Phase.Between:
                    if (m_NextTurnAt >= 0f && Time.time >= m_NextTurnAt) BeginTurn();
                    break;
            }
        }

        void EndRecording()
        {
            if (m_Echoing)
            {
                int round = m_Match.Round; // the round this echo belongs to (scoring moves the match on)
                int points = m_Match.Echoed(m_Recorded, type => InstrumentTarget.Find(type) != null);
                m_Link?.Send("SCORE", $"{round}|{points}");
                Say(UiTerm.BattleGotPoints, points);
                Debug.Log($"[RoomOrchestra] Echo Battle: our echo scored {points}");
                NextTurnSoon();
            }
            else
            {
                m_Match.Called(m_Recorded);
                m_Link?.Send("CALL", $"{m_Match.Round}|{EchoBattleRules.Encode(m_Recorded)}");
                State = Phase.WaitingForThem;
                Say(UiTerm.BattleTheyEcho);
                Debug.Log($"[RoomOrchestra] Echo Battle: our call sent ({EchoBattleRules.Encode(m_Recorded)})");
            }
            ShowScore();
        }

        // Every note played while recording counts (heard from the hit itself, so a full ring never loses one).
        void OnHit(MusicalHitEvent hit)
        {
            if (State != Phase.Recording || m_Recorded.Count >= EchoBattleRules.MaxNotes || conductor?.Clock == null) return;
            conductor.Clock.Nearest(hit.CreatedAtDspTime, out _, out int step);
            m_Recorded.Add(new PhraseNote(hit.Target, hit.Note.Index, step));
        }

        // During a battle every note is a one-shot: it sounded; it comes straight back out of the song (by its own id,
        // so the player's set-aside song is never touched).
        void OnNoteAdded(EchoEvent echo)
        {
            if (!InBattle || conductor == null) return;
            conductor.RemoveEcho(echo.StableId);
        }

        // ---- The card's words ----
        void Say(UiTerm term, params object[] args)
        {
            if (status == null) return;
            string text = UiText.Get(term, Localization.Current);
            status.text = args.Length > 0 ? string.Format(text, args) : text;
        }

        void SetScore(string text)
        {
            if (score != null) score.text = text;
        }

        void ShowScore()
        {
            if (m_Match == null) return;
            var lang = Localization.Current;
            SetScore(string.Format(UiText.Get(UiTerm.BattleRound, lang), Mathf.Min(m_Match.Round, EchoBattleRules.Rounds)) + "\n" +
                     string.Format(UiText.Get(UiTerm.BattleScore, lang), m_Match.MyScore, m_Match.TheirScore));
        }

        // Android delivers broadcast packets only to an app holding a Wi-Fi multicast lock.
        sealed class AndroidMulticast
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidJavaObject m_Lock;
#endif
            public static AndroidMulticast Acquire()
            {
                var held = new AndroidMulticast();
#if UNITY_ANDROID && !UNITY_EDITOR
                try
                {
                    using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    using var wifi = activity.Call<AndroidJavaObject>("getSystemService", "wifi");
                    held.m_Lock = wifi.Call<AndroidJavaObject>("createMulticastLock", "LittleEchoesBattle");
                    held.m_Lock.Call("setReferenceCounted", false);
                    held.m_Lock.Call("acquire");
                }
                catch (System.Exception e) { Debug.LogWarning($"[RoomOrchestra] Echo Battle: no Wi-Fi multicast lock ({e.Message}); finding a host may fail"); }
#endif
                return held;
            }

            public void Release()
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                try { m_Lock?.Call("release"); } catch (System.Exception) { }
                m_Lock = null;
#endif
            }
        }
    }
}
