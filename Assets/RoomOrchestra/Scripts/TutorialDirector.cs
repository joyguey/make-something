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
        [Tooltip("The 3-2-1-Go count over the stage, and its place and size there (it is saved parked).")]
        [SerializeField] Transform countdown;
        [SerializeField] TMP_Text countdownText;
        [SerializeField] Vector3 countdownHome, countdownScale = Vector3.one;
        [Tooltip("Where the bubble and glow wait (inside the ball stand's cloud).")]
        [SerializeField] Transform park;

        [Tooltip("The bubble's place over Flutter and its size (it is saved parked).")]
        [SerializeField] Vector3 bubbleHome, bubbleScale = Vector3.one;

        static readonly Vector3 GlowParked = new Vector3(0.01f, 0.01f, GlowDepth);

        readonly TutorialPlan m_Plan = new();
        int m_Misses, m_ShownBeat = -1;
        bool m_Started, m_Closed, m_BubbleOut, m_GlowOut;
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
            new Color(0.25f, 0.55f, 1f), new Color(0.65f, 0.38f, 0.95f), new Color(1f, 0.78f, 0.2f), new Color(0.3f, 0.82f, 0.38f),
        };

        public void ConfigureCountdown(Transform holder, TMP_Text text)
        {
            countdown = holder;
            countdownText = text;
        }

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
            if (countdown != null)
            {
                countdownHome = countdown.localPosition;
                countdownScale = countdown.localScale;
            }
            ParkBubble();
            ParkGlow();
            ParkCountdown();
        }

        void Update()
        {
            if (m_Closed) return;
            if (!m_Started)
            {
                var intro = IntroSequence.Instance;
                if (intro != null && !intro.Done) return;
                if (Time.timeSinceLevelLoad < JellyBody.StartDelaySeconds) return;
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
                if (!m_Plan.Done) Say(m_Plan.Current.Line);
            }
            if (m_CloseAt >= 0f && Time.time >= m_CloseAt) Close();
            AnimateBubble();
            PlaceGlow();
            ShowCountdown();
        }

        void Begin()
        {
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
            Say(step.Line);
            m_GlowOut = true;
            Debug.Log($"[RoomOrchestra] Tutorial: step {m_Plan.Index + 1}/{TutorialPlan.Song.Length}, {step.Instrument} note {step.GlowNote}");
        }

        InstrumentTarget TargetFor(InstrumentType type)
        {
            if (columns == null) return null;
            foreach (var column in columns)
                if (column != null && column.Target != null && column.Target.Instrument == type) return column.Target;
            return null;
        }

        void OnNoteAdded(EchoEvent echo)
        {
            if (!Active || conductor == null) return;
            var step = m_Plan.Current;
            if (step.OnTime && m_Plan.Wants(echo.Target, echo.NoteIndex))
            {
                conductor.Clock.Nearest(echo.CreatedAtDsp, out _, out int landed);
                var timing = TutorialPlan.Judge(step.BarStep, landed, m_Misses);
                if (timing == TutorialPlan.Timing.OnTime)
                {
                    conductor.MoveEcho(echo.StableId, step.BarStep);
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
                conductor.MoveEcho(echo.StableId, m_Plan.Current.BarStep); // into its place in the tune
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
            Say(UiTerm.TutorialDone);
            m_TryAgainUntil = -1f;
            m_GlowOut = false;
            ParkGlow();
            ParkCountdown();
            m_CloseAt = Time.time + DoneLineSeconds;
        }

        /// <summary>The Skip leaf: everything appears, and Flutter's bubble tucks away.</summary>
        public void Skip()
        {
            if (m_Closed || m_Plan.Done) return;
            m_Plan.Skip();
            Debug.Log("[RoomOrchestra] Tutorial: skipped");
            RevealAll();
            Close();
        }

        /// <summary>No lesson: every station home at once (skipping the opening, and the layout tests' state).</summary>
        public void SkipNow()
        {
            if (!m_Plan.Done) m_Plan.Skip();
            foreach (var station in stations)
                if (station != null) station.ShowNow();
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
        }

        void Close()
        {
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

        void ParkCountdown()
        {
            m_ShownBeat = -1;
            if (countdown == null || park == null) return;
            countdown.position = park.position;
            countdown.localScale = countdownScale * ParkedScale;
        }

        // While the timed note is wanted: the count for this beat of the bar, read from the audio clock (not the
        // frame rate), popping a little bigger as each beat starts and settling. Parked while the music is paused.
        void ShowCountdown()
        {
            if (countdown == null) return;
            bool counting = Active && m_Plan.Current.OnTime && conductor != null && conductor.Clock != null && !conductor.IsPaused;
            if (!counting)
            {
                if (m_ShownBeat >= 0 || (park != null && countdown.position != park.position)) ParkCountdown();
                return;
            }
            double phase = conductor.Clock.BarPhase(AudioSettings.dspTime);
            int beat = TutorialPlan.CountdownBeat(phase);
            if (beat != m_ShownBeat)
            {
                m_ShownBeat = beat;
                if (countdownText != null)
                {
                    countdownText.text = beat < 3 ? (3 - beat).ToString() : UiText.Get(UiTerm.Go, Localization.Current);
                    countdownText.color = CountColours[beat];
                }
                countdown.localPosition = countdownHome;
            }
            float inBeat = (float)(phase * MusicClock.BeatsPerBar - beat);
            float pop = 1f + 0.25f * (1f - inBeat) * (1f - inBeat);
            countdown.localScale = countdownScale * pop;
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
