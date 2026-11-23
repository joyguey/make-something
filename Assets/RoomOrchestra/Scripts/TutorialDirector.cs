using TMPro;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// The guided first song (Version 1 feedback: start with one piece of furniture, a tutorial song; owner,
    /// 2026-10-29: the chair first, Flutter talks, the next thing to tap glows, a Skip button). After the
    /// opening, Flutter's speech bubble asks for one note at a time of "Mary Had a Little Lamb"
    /// (<see cref="TutorialPlan"/>) and the tile to tap glows. A right note snaps into its place in the bar; any
    /// other is taken back with a gentle "try the glowing one". The lamp and then the table appear in a big
    /// poof when the song needs them (<see cref="StationReveal"/>). It listens to the song, not to taps, so the
    /// controller's select-and-place, a thrown ball and a tap all count the same; Undo and Clear step it back.
    /// The last note is played in time: a big 3 (blue), 2 (purple), 1 (gold), Go! (green) counts the bar's four
    /// beats from the music clock, over and over, and the lamp's note should land on Go.
    /// Bubble and glow are authored in the scene and parked inside the ball stand's cloud when not in use.
    /// </summary>
    public class TutorialDirector : MonoBehaviour
    {
        public const float PopSeconds = 0.35f, TryAgainSeconds = 2.5f, DoneLineSeconds = 7f;
        /// <summary>The bubble's and glow's scale, as a share of their own, while parked in the cloud.</summary>
        public const float ParkedScale = 0.05f;
        const float GlowMargin = 0.035f, GlowDepth = 0.006f, PulsePerSecond = 0.7f, PulseSize = 0.08f;

        [SerializeField] EchoConductor conductor;
        [SerializeField] StationReveal[] stations;
        [SerializeField] PitchColumn[] columns;
        [Tooltip("Flutter's speech bubble (its Skip leaf rides on it); it pops out of Flutter.")]
        [SerializeField] Transform bubble;
        [SerializeField] TMP_Text line;
        [SerializeField] Transform speaker;
        [SerializeField] Transform glow;
        [Tooltip("The 3-2-1-Go count: solid 3D words over the stage. The holder stays put; the word on show sits at its " +
                 "origin and the others wait in the cloud. Order: 3, 2, 1, GO!, 开始！")]
        [SerializeField] Transform countdown;
        [SerializeField] Transform[] countPieces;
        [Tooltip("The virtual starting room: it comes apart when the song is done or skipped.")]
        [SerializeField] VirtualRoom room;
        [Tooltip("Bobs above the ball on the cloud whenever the lesson wants a throw (the warm-up and the count).")]
        [SerializeField] Transform arrow;
        [SerializeField] BallLauncher launcher;
        const float ArrowLift = 0.16f, ArrowBob = 0.03f, ArrowBobsPerSecond = 1.2f;
        [Tooltip("Where the bubble and glow wait (inside the ball stand's cloud).")]
        [SerializeField] Transform park;

        [Tooltip("The bubble's place over Flutter and its size (it is saved parked).")]
        [SerializeField] Vector3 bubbleHome, bubbleScale = Vector3.one;

        static readonly Vector3 GlowParked = new Vector3(0.01f, 0.01f, GlowDepth);

        /// <summary>Set once the song is finished or skipped on this device (local only; nothing leaves it).</summary>
        public const string DonePrefKey = "RoomOrchestra.TutorialDone";

        TutorialPlan m_Plan = new();
        bool m_Replaying;

        /// <summary>Has this device finished or skipped the lesson before? Then it starts in free play.</summary>
        public static bool RememberedDone => PlayerPrefs.GetInt(DonePrefKey, 0) == 1;

        static void Remember()
        {
            PlayerPrefs.SetInt(DonePrefKey, 1);
            PlayerPrefs.Save();
        }
        int m_Misses, m_ShownBeat = -1;
        bool m_Started, m_Closed, m_BubbleOut, m_GlowOut, m_Announcing;
        float m_PopStart = -10f, m_TryAgainUntil = -1f, m_CloseAt = -1f;
        UiTerm m_Line;

        public static TutorialDirector Instance { get; private set; }
        public TutorialPlan Plan => m_Plan;
        /// <summary>The lesson is on screen: started, not finished or skipped.</summary>
        public bool Active => m_Started && !m_Plan.Done;
        public bool BubbleOut => m_BubbleOut;
        public bool GlowOut => m_GlowOut;
        public Transform Glow => glow;
        public Transform Bubble => bubble;
        public UiTerm Line => m_Line;
        /// <summary>The count on show now (0 = "3" ... 3 = "Go!"), or -1.</summary>
        public int CountdownBeat => m_ShownBeat;
        public int Misses => m_Misses;

        /// <summary>The count's colours: 3 blue, 2 purple, 1 gold, Go! green (the numbers themselves say which).</summary>
        public static readonly Color[] CountColours =
        {
            new Color(0.902f, 0.706f, 0.133f), new Color(0.902f, 0.706f, 0.133f), new Color(0.902f, 0.706f, 0.133f), new Color(0.361f, 0.722f, 0.380f),
        };
        public const int GoChinese = 4;

        public void ConfigureRoom(VirtualRoom startingRoom) => room = startingRoom;

        public void ConfigureArrow(Transform ballArrow, BallLauncher balls)
        {
            arrow = ballArrow;
            launcher = balls;
        }

        public Transform Arrow => arrow;
        /// <summary>The arrow is pointing at the ball now.</summary>
        public bool ArrowOut { get; private set; }

        public void ConfigureCountdown(Transform holder, Transform[] pieces)
        {
            countdown = holder;
            countPieces = pieces;
        }

        /// <summary>The count word on show now (3, 2, 1 or GO!), or null.</summary>
        public Transform ShownCountPiece { get; private set; }
        public System.Collections.Generic.IReadOnlyList<Transform> CountPieces => countPieces;

        public void Configure(EchoConductor echoes, StationReveal[] hiddenStations, PitchColumn[] pitchColumns,
            Transform speechBubble, TMP_Text speech, Transform flutter, Transform glowPlate, Transform parkPoint)
        {
            conductor = echoes;
            stations = hiddenStations;
            columns = pitchColumns;
            bubble = speechBubble;
            line = speech;
            speaker = flutter;
            glow = glowPlate;
            park = parkPoint;
        }

        void OnEnable()
        {
            Instance = this;
            EchoConductor.NoteAdded += OnNoteAdded;
            Localization.Changed += OnLanguage;
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            EchoConductor.NoteAdded -= OnNoteAdded;
            Localization.Changed -= OnLanguage;
        }

        /// <summary>Records the bubble's place over Flutter and its size, then parks it and the glow (scene setup).</summary>
        public void ParkForSave()
        {
            if (bubble != null)
            {
                bubbleHome = bubble.localPosition;
                bubbleScale = bubble.localScale;
            }
            ParkBubble();
            ParkGlow();
            ParkCountdown();
        }

        void Update()
        {
            KeepParkedInTheCloud();
            if (m_Announcing)
            {
                // a line after the lesson (Announce): out, held, back into Flutter, parked; nothing else runs
                if (m_CloseAt >= 0f && Time.time >= m_CloseAt) Close();
                AnimateBubble();
                if (!m_BubbleOut && !Popping)
                {
                    ParkBubble();
                    m_Closed = true;
                    m_Announcing = false;
                }
                return;
            }
            if (m_Closed) return;
            if (!m_Started)
            {
                var intro = IntroSequence.Instance;
                if (intro != null && !intro.Done) return;
                if (Time.timeSinceLevelLoad < JellyBody.StartDelaySeconds) return;
                if (RememberedDone && !m_Replaying)
                {
                    StartInFreePlay();
                    return;
                }
                Begin();
            }
            if (conductor != null && m_Plan.Sync(conductor.Echoes))
            {
                if (m_Plan.Done) Complete();
                else ShowStep();
            }
            if (m_TryAgainUntil >= 0f && Time.time >= m_TryAgainUntil)
            {
                m_TryAgainUntil = -1f;
                if (!m_Plan.Done) Say(CurrentLine);
            }
            Cheer();
            if (m_CloseAt >= 0f && Time.time >= m_CloseAt) Close();
            AnimateBubble();
            PlaceGlow();
            ShowCountdown();
            PlaceArrow();
        }

        // A returning player: no lesson. Everything appears in its poof and the virtual room comes apart at once.
        void StartInFreePlay()
        {
            m_Plan.Skip();
            m_Closed = true;
            RevealAll();
            if (room != null) room.TearDown();
            Debug.Log("[RoomOrchestra] Tutorial: done before on this device; straight to free play (Lesson replays it)");
        }

        /// <summary>
        /// The leaf's Lesson button: the guided song again, from the first note. The song so far is cleared (Undo
        /// brings it back) and the lamp and table go back into the cloud in a poof until the song needs them.
        /// </summary>
        public void Replay()
        {
            if (Active) return;
            Debug.Log("[RoomOrchestra] Tutorial: replaying the lesson");
            RealFurniture.Instance?.PutBack(); // the instruments leave the real furniture before their stations hide
            m_Announcing = false;
            conductor?.Clear();
            foreach (var station in stations)
                if (station != null && !station.Hidden)
                {
                    PoofCloud.PoofAt(station.PoofCentre, JungleDirector.ClearSmokeSize * 0.5f);
                    station.HideNow();
                }
            TuckAway.TuckAll(); // one thing to try again: the leaf's extras wait under it until the song is done
            var chair = TargetFor(TutorialPlan.Song[0].Instrument);
            if (chair != null) OrbSelector.Instance?.Focus(chair);
            m_Plan = new TutorialPlan();
            m_Misses = 0;
            m_CheerHops = 0;
            m_CloseAt = -1f;
            m_ClearSongOnClose = false;
            m_TryAgainUntil = -1f;
            m_Closed = false;
            m_Started = false;
            m_Replaying = true; // Update begins it at once (the opening is over)
        }

        Transform m_Skip;
        Vector3 m_SkipScale;

        // The Skip leaf on the bubble: shrunk out of sight (inside the card) for a line after the lesson.
        void ShowSkip(bool on)
        {
            if (m_Skip == null && bubble != null)
            {
                m_Skip = bubble.Find("SkipButton");
                if (m_Skip != null) m_SkipScale = m_Skip.localScale;
            }
            if (m_Skip == null) return;
            var want = on ? m_SkipScale : m_SkipScale * 0.05f;
            if (m_Skip.localScale != want) m_Skip.localScale = want;
        }

        void Begin()
        {
            ShowSkip(true);
            ShowCloseX(false);
            InstrumentPacks.Instance?.ResetPlaces(); // the lesson is about the chair's mbira, the lamp's erhu, the table's udu
            // Notes played during the opening (a ball thrown before Flutter spoke) are not part of the tune: one sat
            // where "had" belongs and jammed the lesson (owner, emulator).
            conductor?.StartFresh();
            m_Started = true;
            Debug.Log("[RoomOrchestra] Tutorial: Flutter starts the first song");
            m_BubbleOut = true;
            m_PopStart = Time.time;
            BubbleTaps(true);
            ShowStep();
        }

        void ShowStep()
        {
            var step = m_Plan.Current;
            foreach (var station in stations)
                if (station != null && station.Hidden && TutorialPlan.IntroducedBy(station.Instrument, m_Plan.Index))
                {
                    station.Reveal();
                    var target = TargetFor(station.Instrument);
                    if (target != null) OrbSelector.Instance?.Focus(target); // the controller's focus moves to the newcomer
                }
            m_TryAgainUntil = -1f;
            Say(CurrentLine);
            m_GlowOut = true;
            if (m_Plan.InWarmup) Debug.Log("[RoomOrchestra] Tutorial: warm-up, toss Flutter at the chair (or tap a tile)");
            else Debug.Log($"[RoomOrchestra] Tutorial: step {m_Plan.Index + 1}/{TutorialPlan.Song.Length}, {step.Instrument} note {step.GlowNote}");
        }

        InstrumentTarget TargetFor(InstrumentType type)
        {
            if (columns == null) return null;
            foreach (var column in columns)
                if (column != null && column.Target != null && column.Target.Instrument == type) return column.Target;
            return null;
        }

        // A right note goes to its place in the bar; anything else already there (not part of the tune) makes room.
        void SnapIntoPlace(EchoEvent echo, int step)
        {
            if (conductor.MoveEcho(echo.StableId, step)) return;
            conductor.RemoveAt(echo.Target, step);
            if (!conductor.MoveEcho(echo.StableId, step))
                Debug.LogWarning($"[RoomOrchestra] Tutorial: couldn't put {echo} on step {step}");
        }

        UiTerm CurrentLine => m_Plan.InWarmup ? UiTerm.TutorialWarmup : m_Plan.Current.Line;

        void OnNoteAdded(EchoEvent echo)
        {
            if (!Active || conductor == null) return;
            if (m_Plan.InWarmup)
            {
                // The warm-up note sounded; it isn't part of the song, so it goes back out and the song begins.
                conductor.Undo();
                if (m_Plan.CompletesWarmup(echo.Target))
                {
                    m_Plan.FinishWarmup();
                    Debug.Log($"[RoomOrchestra] Tutorial: warm-up done ({echo.Target} note {echo.NoteIndex})");
                    ShowStep();
                }
                else
                {
                    Say(UiTerm.TutorialTryAgain);
                    m_TryAgainUntil = Time.time + TryAgainSeconds;
                }
                return;
            }
            var step = m_Plan.Current;
            if (step.OnTime && m_Plan.Wants(echo.Target, echo.NoteIndex))
            {
                conductor.Clock.Nearest(echo.CreatedAtDsp, out _, out int landed);
                var timing = TutorialPlan.Judge(step.BarStep, landed, m_Misses);
                if (timing == TutorialPlan.Timing.OnTime)
                {
                    SnapIntoPlace(echo, step.BarStep);
                    Debug.Log($"[RoomOrchestra] Tutorial: on Go (landed on step {landed}, {m_Misses} misses before)");
                    return;
                }
                m_Misses++;
                conductor.Undo();
                Say(timing == TutorialPlan.Timing.Early ? UiTerm.TutorialEarly : UiTerm.TutorialLate);
                m_TryAgainUntil = Time.time + TryAgainSeconds;
                Debug.Log($"[RoomOrchestra] Tutorial: {timing} (step {landed}, Go is {step.BarStep}); miss {m_Misses}");
                return;
            }
            if (m_Plan.Wants(echo.Target, echo.NoteIndex))
            {
                SnapIntoPlace(echo, m_Plan.Current.BarStep); // into its place in the tune
                return;                                                     // Update follows the song
            }
            // Not the note asked for: it played, and is taken straight back out of the song.
            conductor.Undo();
            Say(UiTerm.TutorialTryAgain);
            m_TryAgainUntil = Time.time + TryAgainSeconds;
            Debug.Log($"[RoomOrchestra] Tutorial: {echo.Target} note {echo.NoteIndex} isn't the one asked for; try again");
        }

        void Complete()
        {
            Debug.Log("[RoomOrchestra] Tutorial: the first song is finished");
            RevealAll();
            if (room != null) room.TearDown(); // the virtual room comes apart: the player's own room is the stage now
            Say(UiTerm.TutorialDone);
            m_TryAgainUntil = -1f;
            m_GlowOut = false;
            ParkGlow();
            ParkCountdown();
            Remember();
            m_ClearSongOnClose = true;
            m_CloseAt = Time.time + DoneLineSeconds;
            // A clear ending (owner: "I didn't know when the tutorial was over"): the friends beam and bounce
            // a few times and every sparkle flares, while Flutter says it's done and play is free from now on.
            m_CheerHops = CheerHops;
            m_NextCheer = Time.time;
            foreach (var sparkle in FindObjectsByType<SparkleTwinkle>(FindObjectsSortMode.None)) sparkle.Flare();
        }

        public const int CheerHops = 3;
        public const float CheerGapSeconds = 0.4f;
        int m_CheerHops;
        float m_NextCheer;
        /// <summary>Hops still to come in the ending's cheer.</summary>
        public int CheerHopsLeft => m_CheerHops;

        void Cheer()
        {
            if (m_CheerHops <= 0 || Time.time < m_NextCheer) return;
            m_CheerHops--;
            m_NextCheer = Time.time + CheerGapSeconds;
            foreach (var token in FindObjectsByType<OrbToken>(FindObjectsSortMode.None))
            {
                var face = token.GetComponentInChildren<CharacterFace>();
                if (face != null) face.Beam();
                var body = token.GetComponentInChildren<JellyBody>();
                if (body != null) body.Kick(0.6f);
            }
        }

        /// <summary>The Skip leaf: everything appears, and Flutter's bubble tucks away.</summary>
        public void Skip()
        {
            if (m_Closed || m_Plan.Done) return;
            m_Plan.Skip();
            Remember();
            Debug.Log("[RoomOrchestra] Tutorial: skipped");
            RevealAll();
            if (room != null) room.TearDown();
            Close();
        }

        /// <summary>No lesson: every station home at once (skipping the opening, and the layout tests' state).</summary>
        public void SkipNow()
        {
            if (!m_Plan.Done) m_Plan.Skip();
            foreach (var station in stations)
                if (station != null) station.ShowNow();
            TuckAway.ShowAllNow();
            if (room != null) room.HideNow();
            m_BubbleOut = false;
            m_GlowOut = false;
            ParkBubble();
            ParkGlow();
            ParkCountdown();
            m_Closed = true;
        }

        void RevealAll()
        {
            foreach (var station in stations)
                if (station != null) station.Reveal();
            TuckAway.RevealAll(); // and the leaf's extras the lesson kept tucked away
        }

        // Owner, headset 2026-11-08: "the tune from Mary had a little lamb continues to play after the tutorial, please
        // clear it so the player can start with a clean plate". The finished song plays on while Flutter says the lesson
        // is done; when that line closes, the song is cleared (not undoable: it was the lesson's, not the player's).
        bool m_ClearSongOnClose;

        void Close()
        {
            if (m_ClearSongOnClose)
            {
                m_ClearSongOnClose = false;
                conductor?.StartFresh();
                JungleDirector.Instance?.ClearForFreshStart(); // and what it grew: flowers, toadstools, vines, butterflies
                Debug.Log("[RoomOrchestra] Tutorial: the lesson's song and what it grew are cleared; the player starts with a clean plate");
            }
            m_CloseAt = -1f;
            m_GlowOut = false;
            ParkGlow();
            if (m_BubbleOut)
            {
                BubbleTaps(false);
                m_BubbleOut = false;
                m_PopStart = Time.time; // shrinks back into Flutter, then waits in the cloud
            }
            m_Closed = !m_BubbleOut && !Popping;
        }

        bool Popping => Time.time - m_PopStart < PopSeconds;

        /// <summary>
        /// Flutter says one line after the lesson (finding the real furniture): the bubble pops out for
        /// <paramref name="seconds"/> with no Skip, then tucks away again.
        /// </summary>
        public void Announce(UiTerm term, float seconds)
        {
            if (Active) return;
            ShowSkip(false); // nothing to skip: the leaf would only confuse (emulator, 2026-11-07)
            Say(term);
            if (!m_BubbleOut)
            {
                m_BubbleOut = true;
                m_PopStart = Time.time;
            }
            BubbleTaps(false);
            ShowCloseX(true);
            m_Announcing = true;
            m_CloseAt = Time.time + seconds;
            Debug.Log($"[RoomOrchestra] Flutter says: {UiText.Get(term, UiLanguage.English)}");
        }

        /// <summary>The x on the bubble (or something that needs the space, like the Echo Battle card): a line after the
        /// lesson goes back into Flutter now. The lesson's own lines stay (Skip ends the lesson).</summary>
        public void Dismiss()
        {
            if (!m_Announcing || !m_BubbleOut) return;
            Debug.Log("[RoomOrchestra] Flutter's bubble: closed with its x");
            Close();
        }

        Transform m_CloseX;

        // The x shows (and takes taps) only on a line after the lesson.
        void ShowCloseX(bool on)
        {
            if (m_CloseX == null && bubble != null) m_CloseX = bubble.Find("BubbleCloseButton");
            if (m_CloseX == null) return;
            m_CloseX.gameObject.SetActive(on);
            foreach (var c in m_CloseX.GetComponentsInChildren<Collider>(true)) c.enabled = on;
        }

        void Say(UiTerm term)
        {
            m_Line = term;
            if (line != null) line.text = UiText.Get(term, Localization.Current);
        }

        void OnLanguage(UiLanguage language)
        {
            if (line != null && m_Started) line.text = UiText.Get(m_Line, language);
            m_ShownBeat = -1; // the count's "Go!" redrawn in the new language on the next frame
        }

        // Pops out of Flutter and grows to full size; at the end, shrinks back into Flutter and parks.
        void AnimateBubble()
        {
            if (bubble == null || !Popping)
            {
                if (!m_BubbleOut && m_Started && !BubbleParked) ParkBubble();
                if (m_CloseAt < 0f && !m_BubbleOut && m_Plan.Done) m_Closed = true;
                return;
            }
            float t = Mathf.Clamp01((Time.time - m_PopStart) / PopSeconds);
            float u = t * t * (3f - 2f * t);
            if (!m_BubbleOut) u = 1f - u;
            var from = speaker != null && bubble.parent != null ? bubble.parent.InverseTransformPoint(speaker.position) : bubbleHome;
            bubble.localPosition = Vector3.Lerp(from, bubbleHome, u);
            bubble.localScale = bubbleScale * Mathf.Lerp(ParkedScale, 1f, u);
        }

        // The ball stand travels with the leaf (Come here) and Height lifts the controls, long after the lesson: what
        // waits in the cloud goes with it, or it is left in mid-air (owner, emulator: the tiny count words under the
        // leaf). Written only when the cloud has moved.
        void KeepParkedInTheCloud()
        {
            if (park == null) return; // at once: the stage recentres a second in (PICO XR)
            if (bubble != null && !m_BubbleOut && !Popping && !BubbleParked) ParkBubble();
            if (glow != null && !m_GlowOut && glow.position != park.position) ParkGlow();
            if (arrow != null && (!Active || !ArrowOut) && (ArrowOut || arrow.position != park.position)) ParkArrow();
            if (countPieces != null && m_ShownBeat < 0)
                foreach (var piece in countPieces)
                    if (piece != null && piece.position != park.position) ParkPiece(piece);
        }

        bool BubbleParked => bubble != null && park != null && bubble.position == park.position;

        // Parked inside the cloud, the Skip leaf must not catch taps meant for the ball stand.
        void BubbleTaps(bool on)
        {
            if (bubble == null) return;
            foreach (var c in bubble.GetComponentsInChildren<Collider>(true)) c.enabled = on;
        }

        void ParkBubble()
        {
            BubbleTaps(false);
            ShowCloseX(false);
            if (bubble == null || park == null) return;
            bubble.position = park.position;
            bubble.localScale = bubbleScale * ParkedScale;
        }

        void ParkGlow()
        {
            if (glow == null || park == null) return;
            glow.position = park.position;
            glow.localScale = GlowParked;
        }

        // Above the ready ball, pointing down at it, bobbing gently, while the lesson wants a throw.
        void PlaceArrow()
        {
            if (arrow == null) return;
            var ball = launcher != null ? launcher.ReadyBall : null;
            bool wanted = Active && (m_Plan.InWarmup || m_Plan.Current.OnTime) && ball != null;
            if (!wanted)
            {
                if (ArrowOut || (park != null && arrow.position != park.position)) ParkArrow();
                return;
            }
            ArrowOut = true;
            float bob = ArrowBob * Mathf.Sin(Time.time * ArrowBobsPerSecond * 2f * Mathf.PI);
            var at = ball.transform.position + Vector3.up * (ArrowLift + bob);
            var eye = Camera.main != null ? Camera.main.transform.position : new Vector3(0f, 1.15f, 0f); // else the seat
            var toEye = eye - at;
            toEye.y = 0f;
            arrow.SetPositionAndRotation(at, Quaternion.LookRotation(toEye.sqrMagnitude > 1e-6f ? -toEye : Vector3.forward, Vector3.up));
            arrow.localScale = Vector3.one;
        }

        void ParkArrow()
        {
            ArrowOut = false;
            if (arrow == null || park == null) return;
            arrow.position = park.position;
            arrow.localScale = Vector3.one * ParkedScale;
        }

        void ParkCountdown()
        {
            m_ShownBeat = -1;
            ShownCountPiece = null;
            if (countPieces == null || park == null) return;
            foreach (var piece in countPieces) ParkPiece(piece);
        }

        void ParkPiece(Transform piece)
        {
            if (piece == null || park == null) return;
            piece.position = park.position;
            piece.localScale = Vector3.one * ParkedScale;
        }

        Transform PieceFor(int beat) =>
            beat < 3 ? countPieces[beat] : countPieces[Localization.Current == UiLanguage.Chinese && countPieces.Length > GoChinese ? GoChinese : 3];

        // While the timed note is wanted: the word for this beat of the bar, read from the audio clock (not the
        // frame rate), popping a little bigger as each beat starts and settling. Parked while the music is paused.
        void ShowCountdown()
        {
            if (countdown == null || countPieces == null || countPieces.Length < 4) return;
            bool counting = Active && m_Plan.Current.OnTime && conductor != null && conductor.Clock != null && !conductor.IsPaused;
            if (!counting)
            {
                if (m_ShownBeat >= 0 || (park != null && countPieces[0].position != park.position)) ParkCountdown();
                return;
            }
            double phase = conductor.Clock.BarPhase(AudioSettings.dspTime);
            int beat = TutorialPlan.CountdownBeat(phase);
            if (beat != m_ShownBeat)
            {
                ParkCountdown();
                m_ShownBeat = beat;
                ShownCountPiece = PieceFor(beat);
                ShownCountPiece.localPosition = Vector3.zero;
            }
            float inBeat = (float)(phase * MusicClock.BeatsPerBar - beat);
            float pop = 1f + 0.25f * (1f - inBeat) * (1f - inBeat);
            ShownCountPiece.localScale = Vector3.one * pop;
        }

        // Behind the wanted tile, a little bigger than it, breathing slowly (no flashing).
        void PlaceGlow()
        {
            if (glow == null) return;
            if (!m_GlowOut)
            {
                if (park != null && glow.position != park.position) ParkGlow();
                return;
            }
            var tile = WantedTile();
            if (tile == null) return;
            var size = tile.lossyScale;
            float pulse = 1f + PulseSize * (0.5f + 0.5f * Mathf.Sin(Time.time * PulsePerSecond * 2f * Mathf.PI));
            glow.SetPositionAndRotation(tile.position + tile.forward * (size.z * 0.5f + GlowDepth), tile.rotation);
            var parentScale = glow.parent != null ? glow.parent.lossyScale : Vector3.one;
            glow.localScale = new Vector3((size.x + GlowMargin) * pulse / parentScale.x, (size.y + GlowMargin) * pulse / parentScale.y, GlowDepth / parentScale.z);
        }

        /// <summary>The tile the glow sits behind now, or null.</summary>
        public Transform WantedTile()
        {
            if (m_Plan.Done || columns == null) return null;
            var step = m_Plan.Current;
            foreach (var column in columns)
                if (column != null && column.Target != null && column.Target.Instrument == step.Instrument)
                    return column.Tiles[step.GlowNote].transform;
            return null;
        }
    }
}
