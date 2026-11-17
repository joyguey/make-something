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
    /// </summary>
    public class EchoBattleDirector : MonoBehaviour
    {
        public const float ParkedScale = 0.12f, ResultSeconds = 2.5f; // 12%: the street card still fits the cloud, and its thinnest drip stays above 1 mm

        public enum Phase { Hidden, Menu, Pairing, CountIn, Recording, WaitingForThem, Listening, Between, Over }

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
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            Localization.Changed -= ShowTitle;
            EchoConductor.NoteAdded -= OnNoteAdded;
            MusicalHitPipeline.Committed -= OnHit;
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
                Say(UiTerm.BattleGone);
                return;
            }
            State = Phase.Pairing;
            Say(role == EchoBattleLink.Role.Host ? UiTerm.BattleWaiting : UiTerm.BattleLooking);
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
            conductor?.SetSongMuted(false); // back to the player's own song
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
                case "JOIN": // we host: the guest is in; we call first
                    m_Link.Send("START");
                    StartMatch(isHost: true);
                    break;
                case "START":
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
            switch (State)
            {
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
