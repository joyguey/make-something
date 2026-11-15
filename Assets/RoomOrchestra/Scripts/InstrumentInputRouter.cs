using UnityEngine;
using UnityEngine.InputSystem;

namespace RoomOrchestra
{
    /// <summary>
    /// Single input path for the room.
    /// Device (PICO XR mode): each controller points a ray; the trigger taps what the ray touches (a tray orb, a
    /// note, a button, an instrument), and on the ready ball it grabs: hold, swing and let go to throw it (a quick
    /// pull is a tap toss; pulling back is a slingshot; aim assist snaps throws to the nearest instrument).
    /// Unity Editor only (controller simulator): click like a tap, click elsewhere to throw toward
    /// the cursor; Left/Right choose orb, Up/Down choose target, Enter places, Q changes intensity,
    /// P changes the pitch petal, E toggles echo edit mode (then arrows nudge/select, Enter removes,
    /// Q undoes); keys 1/2/3 place on table/chair/lamp, Space throws straight ahead, C clears echoes,
    /// Z undoes, K pauses/resumes the music, G calls the leaf pad, L switches language, H height.
    /// </summary>
    public class InstrumentInputRouter : MonoBehaviour
    {
        [SerializeField] BallLauncher launcher;
        [Tooltip("Editor-only preview camera used for mouse input; falls back to Camera.main.")]
        [SerializeField] Camera editorCamera;
        [SerializeField, Min(0.1f)] float editorThrowSpeed = 7f;
        [Tooltip("Repeat taps on the same object within this window (seconds) are ignored (a trigger that bounces).")]
        [SerializeField, Min(0f)] float tapDebounce = 0.2f;
        [Header("Pointer rays (PICO XR)")]
        [Tooltip("Left and right: the ray each controller points.")]
        [SerializeField] LineRenderer[] rays;
        [Tooltip("Left and right: the dot where the ray touches something.")]
        [SerializeField] Transform[] reticles;
        [Tooltip("The ray's look over nothing in particular, and over something it can tap or grab.")]
        [SerializeField] Material rayIdle, rayHot;

        /// <summary>How far a pointer reaches (m), and how long its ray is drawn when it touches nothing.</summary>
        public const float RayReach = 20f, RayIdleLength = 3f;

        /// <summary>The controllers (a fake in tests).</summary>
        public IPointerSource Pointers { get; set; }

        static readonly Vector3[] s_Palm = new Vector3[2];
        static readonly bool[] s_PalmTracked = new bool[2], s_BareHand = new bool[2];
        static bool s_Hands; // the last kind of input seen: bare hands, or controllers

        /// <summary>
        /// Where each hand is this frame (scene space): the palm of a bare hand, or the controller in it. Things you can
        /// touch read it (the title sign rocks when pushed; its handprint starts the game).
        /// </summary>
        public static bool TryGetPalm(int hand, out Vector3 palm)
        {
            palm = s_Palm[hand];
            return s_PalmTracked[hand];
        }

        /// <summary>The player is playing with bare hands (look and pinch) rather than controllers. It holds while the
        /// hands are out of the cameras' sight (resting in the lap, behind the back): only a tracked controller ends it.</summary>
        public static bool UsingHands => s_Hands;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPalms()
        {
            for (int i = 0; i < 2; i++) s_PalmTracked[i] = s_BareHand[i] = false;
            s_Hands = false;
        }
        readonly bool[] m_WasSelect = new bool[2];
        readonly bool[] m_Grabbing = new bool[2];
        readonly float[] m_GrabDistance = new float[2];

        public void ConfigurePointers(LineRenderer[] handRays, Transform[] handReticles, Material idle, Material hot)
        {
            rays = handRays;
            reticles = handReticles;
            rayIdle = idle;
            rayHot = hot;
        }

        void OnEnable() => MusicalHitPipeline.Committed += FeelHit;
        void OnDisable() => MusicalHitPipeline.Committed -= FeelHit;
        static void FeelHit(MusicalHitEvent hit) => Haptics.Hit(hit.Orb);

        void Update()
        {
#if UNITY_EDITOR
            if (Pointers != null) for (int hand = 0; hand < 2; hand++) Point(hand);
            HandleEditorInput();
#else
            Pointers ??= new XRPointerSource();
            for (int hand = 0; hand < 2; hand++) Point(hand);
#endif
        }

        /// <summary>The nearest thing a pointer ray touches, skipping balls already in flight.</summary>
        public static bool Aim(Ray ray, out RaycastHit hit)
        {
            hit = default;
            var hits = Physics.RaycastAll(ray, RayReach, ~(1 << RealWalls.SurfaceLayer), QueryTriggerInteraction.Collide); // the room's surface only while choosing places
            float best = float.MaxValue;
            bool any = false;
            foreach (var h in hits)
            {
                var ball = h.collider.GetComponentInParent<Ball>();
                if (ball != null && ball.InFlight) continue;
                if (h.distance < best)
                {
                    best = h.distance;
                    hit = h;
                    any = true;
                }
            }
            return any;
        }

        // One controller: its ray, then a press (tap or grab), a hold (drag the ball) or a release (throw it).
        void Point(int hand)
        {
            var frame = Pointers.Read(hand);
            var rig = XrRig.Instance != null ? XrRig.Instance.transform : null;
            s_PalmTracked[hand] = frame.Tracked;
            s_BareHand[hand] = frame.Gaze;
            if (frame.Tracked)
            {
                s_Palm[hand] = rig != null ? rig.TransformPoint(frame.GripPosition) : frame.GripPosition;
                s_Hands = frame.Gaze;
            }
            if (!frame.Tracked)
            {
                if (m_Grabbing[hand] && launcher != null) launcher.EndGrab(GrabId(hand), Vector3.zero); // lost: let go
                m_Grabbing[hand] = false;
                m_WasSelect[hand] = false;
                m_Hovering[hand] = null;
                HoverLight.Set(hand, null);
                ShowRay(hand, false, default, default, false, false);
                return;
            }
            var origin = rig != null ? rig.TransformPoint(frame.Position) : frame.Position;
            var dir = (rig != null ? rig.rotation * frame.Rotation : frame.Rotation) * Vector3.forward;
            // look and pinch (hands): the ray is the gaze; a pinched ball moves with the hand itself
            m_Gaze[hand] = frame.Gaze;
            var handAt = frame.Gaze ? (rig != null ? rig.TransformPoint(frame.HandPosition) : frame.HandPosition) : origin;
            var ray = new Ray(origin, dir);
            bool pressed = frame.Select && !m_WasSelect[hand];
            bool released = !frame.Select && m_WasSelect[hand];
            m_WasSelect[hand] = frame.Select;

            if (m_Grabbing[hand])
            {
                m_Hovering[hand] = null; // the held ball is no longer "under the pointer"
                HoverLight.Set(hand, null);
                var held = frame.Gaze ? handAt : origin + dir * m_GrabDistance[hand];
                if (released)
                {
                    m_Grabbing[hand] = false;
                    launcher.EndGrab(GrabId(hand), dir);
                    if (!frame.Gaze) Haptics.Pulse(hand, Haptics.ThrowStrength, Haptics.ThrowMs);
                }
                else launcher.UpdateGrab(GrabId(hand), held, dir);
                ShowRay(hand, true, origin, held, true, true);
                return;
            }

            if (Choose(hand, ray, origin, pressed)) return;

            bool touching = Aim(ray, out var hit);
            var target = touching ? hit.collider.gameObject : null;
            bool ball = launcher != null && launcher.IsReadyBall(target);
            var owner = target != null ? PressableOwner(target) : null;
            bool tappable = ball || owner != null;
            // a soft tick as the pointer comes onto something new to press (not the instruments' own notes)
            var hoverOn = tappable && !(owner is InstrumentTarget) ? (owner != null ? (Component)owner : hit.collider) : null;
            if (hoverOn != m_Hovering[hand])
            {
                m_Hovering[hand] = hoverOn;
                if (hoverOn != null) UiSounds.Play(UiSound.Hover, hit.point);
                HoverLight.Set(hand, hoverOn != null ? HoverLight.RendererOf(hit.collider) : null); // and it lights up
            }
            ShowRay(hand, true, origin, touching ? hit.point : origin + dir * RayIdleLength, touching, tappable);
            // on an instrument's real object: its five note bands show, the one under the pointer lit (owner, 2026-11-08)
            if (owner is InstrumentTarget instrument && instrument.Home != null) instrument.Home.Point(hit.point);
            if (!pressed || target == null) return;
            PressPoint = hit.point;
            if (ball)
            {
                m_Grabbing[hand] = true;
                m_GrabDistance[hand] = hit.distance;
                launcher.BeginGrab(GrabId(hand), frame.Gaze ? handAt : hit.point);
                if (!frame.Gaze) Haptics.Pulse(hand, Haptics.GrabStrength, Haptics.GrabMs);
            }
            else if (!IsDuplicateTap(target))
            {
                if (!frame.Gaze) Haptics.Pulse(hand, Haptics.PressStrength, Haptics.PressMs); // the press landed
                Activate(target);
            }
        }

        static int GrabId(int hand) => 100 + hand;
        readonly bool[] m_Gaze = new bool[2];
        readonly Component[] m_Hovering = new Component[2];

        // After the lesson the player chooses where each instrument lives (owner, headset 2026-11-08): the ray touches
        // the scanned room, a glowing spot previews the place, and a press puts the instrument there. A press on that
        // instrument itself keeps it on its virtual furniture. The leaf's buttons and the ball still work as always.
        int m_PreviewHand = -1;

        bool Choose(int hand, Ray ray, Vector3 origin, bool pressed)
        {
            var placer = RealFurniture.Instance;
            if (placer == null || !placer.Choosing)
            {
                m_PreviewHand = -1;
                return false;
            }
            bool ui = Aim(ray, out var near);
            var owner = ui ? PressableOwner(near.collider.gameObject) : null;
            if (owner is RealSpot) owner = null; // the preview spot itself
            bool keep = owner is InstrumentTarget t && t == placer.ChoosingTarget;
            // Something tappable along the ray wins over the room's surface: nearby things are never hidden by the room
            // (RealWalls.NearClear), so what the player sees under the ray is what they press.
            RaycastHit room = default;
            bool roomFirst = owner == null
                             && Physics.Raycast(ray, out room, RayReach, 1 << RealWalls.SurfaceLayer, QueryTriggerInteraction.Ignore);
            if (keep)
            {
                if (m_PreviewHand == hand) placer.HidePreview();
                ShowRay(hand, true, origin, near.point, true, true);
                if (pressed) placer.KeepVirtual();
                return true;
            }
            if (roomFirst)
            {
                placer.Preview(room.point);
                m_PreviewHand = hand;
                ShowRay(hand, true, origin, room.point, true, true);
                if (pressed) placer.PlaceAt(room.point);
                return true;
            }
            if (m_PreviewHand == hand)
            {
                placer.HidePreview(); // this hand's ray left the room's surface
                m_PreviewHand = -1;
            }
            return false; // a button, the ball or nothing: as always
        }

        void ShowRay(int hand, bool on, Vector3 from, Vector3 to, bool touching, bool hot)
        {
            var line = rays != null && hand < rays.Length ? rays[hand] : null;
            if (m_Gaze[hand] && line != null && line.enabled) line.enabled = false; // look and pinch: a dot where you look, no beam
            if (m_Gaze[hand]) line = null;
            var dot = reticles != null && hand < reticles.Length ? reticles[hand] : null;
            if (line != null)
            {
                if (line.enabled != on) line.enabled = on;
                if (on)
                {
                    line.SetPosition(0, from);
                    line.SetPosition(1, to);
                    var look = hot ? rayHot : rayIdle;
                    if (look != null && line.sharedMaterial != look) line.sharedMaterial = look;
                }
            }
            if (dot != null)
            {
                bool showDot = on && touching;
                if (dot.gameObject.activeSelf != showDot) dot.gameObject.SetActive(showDot);
                if (showDot) dot.position = to;
                if (showDot && dot.TryGetComponent<Renderer>(out var r))
                {
                    var look = hot ? rayHot : rayIdle;
                    if (look != null && r.sharedMaterial != look) r.sharedMaterial = look;
                }
            }
        }

        /// <summary>The pressable thing that owns <paramref name="hit"/> (a button, tile, orb, character...), or null.</summary>
        public static Component PressableOwner(GameObject hit)
        {
            if (hit == null) return null;
            Component owner = hit.GetComponentInParent<ConfirmChoice>();
            if (owner == null) owner = hit.GetComponentInParent<RealSpot>();
            if (owner == null) owner = hit.GetComponentInParent<LoopControlButton>();
            if (owner == null) owner = hit.GetComponentInParent<InstrumentButton>();
            if (owner == null) owner = hit.GetComponentInParent<OrbToken>();
            if (owner == null) owner = hit.GetComponentInParent<TierButton>();
            if (owner == null) owner = hit.GetComponentInParent<PitchTile>();
            if (owner == null) owner = hit.GetComponentInParent<UndoButton>();
            if (owner == null) owner = hit.GetComponentInParent<MusicControlButton>();
            if (owner == null) owner = hit.GetComponentInParent<RecenterButton>();
            if (owner == null) owner = hit.GetComponentInParent<MixButton>();
            if (owner == null) owner = hit.GetComponentInParent<HeightButton>();
            if (owner == null) owner = hit.GetComponentInParent<LanguageButton>();
            if (owner == null) owner = hit.GetComponentInParent<TrayCallButton>();
            if (owner == null) owner = hit.GetComponentInParent<TitleStartButton>();
            if (owner == null) owner = hit.GetComponentInParent<QuitButton>();
            if (owner == null) owner = hit.GetComponentInParent<TutorialSkipButton>();
            if (owner == null) owner = hit.GetComponentInParent<TutorialReplayButton>();
            if (owner == null) owner = hit.GetComponentInParent<ChangeInstrumentButton>();
            if (owner == null) owner = hit.GetComponentInParent<SoundPackButton>();
            if (owner == null) owner = hit.GetComponentInParent<ViewButton>();
            if (owner == null) owner = hit.GetComponentInParent<EchoBattleButton>();
            if (owner == null) owner = hit.GetComponentInParent<EchoMini>();
            if (owner == null) owner = hit.GetComponentInParent<IPressable>() as Component; // Menu and its newer buttons
            if (owner == null) owner = InstrumentTarget.Of(hit.transform); // its furniture, or the real object it lives on
            return owner;
        }

        /// <summary>True if this tap repeats a tap on the same object within <see cref="tapDebounce"/>.</summary>
        bool IsDuplicateTap(GameObject hit)
        {
            var owner = PressableOwner(hit);
            if (owner == null) return false;

            if (MusicalHitPipeline.TryAcceptRepeat($"tap:{owner.GetInstanceID()}", tapDebounce))
                return false;
            Debug.Log($"[RoomOrchestra] Ignored duplicate tap on {owner.name}");
            return true;
        }

        /// <summary>
        /// Activates what owns <paramref name="hit"/>: loop button, button, tray orb, intensity
        /// button, or an instrument (placing the selected orb on it).
        /// </summary>
        public static bool Activate(GameObject hit)
        {
            var owner = PressableOwner(hit);
            if (owner is ConfirmChoice choice)
            {
                // the confirm card's answer: do the press it asked about, or let it go
                var card = ConfirmCard.Instance;
                var pending = card != null ? card.Pending : null;
                Acknowledge(choice);
                UiSounds.Play(choice.Yes ? UiSound.Confirm : UiSound.Cancel, choice);
                s_Confirm.Cancel();
                AskOn(null);
                if (choice.Yes && pending != null)
                {
                    Debug.Log($"[RoomOrchestra] Confirm card: yes, {pending.name}");
                    Dispatch(pending.gameObject);
                    Acknowledge(pending);
                }
                else Debug.Log("[RoomOrchestra] Confirm card: cancelled");
                return true;
            }
            if (NeedsConfirm(owner))
            {
                if (!s_Confirm.Request(owner.GetInstanceID()))
                {
                    AskOn(owner); // first tap: the card asks what will happen (and the label says "Sure?"); nothing happens yet
                    Acknowledge(owner);
                    return true;
                }
            }
            else if (owner != null) s_Confirm.Cancel(); // pressing anything else is a "no"
            AskOn(null);
            if (!Dispatch(hit)) return false;
            Acknowledge(owner);
            return true;
        }

        static readonly ConfirmGate s_Confirm = new();
        static LocalizedLabel s_AskingLabel;

        /// <summary>The button now waiting for its second tap, if any (for tests and the log).</summary>
        public static int ConfirmAsking => s_Confirm.Asking;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetConfirm()
        {
            s_Confirm.Cancel();
            s_AskingLabel = null;
        }

        /// <summary>
        /// Presses that end or wipe something ask first (George, 2026-11-03): Clear with a song to lose, Lesson (starts the
        /// song over), Start over, Quit, and Leave during a battle. Not while the title card is up (there only Start and Quit act).
        /// </summary>
        static bool NeedsConfirm(Component owner)
        {
            if (owner == null) return false;
            var title = TitleScreen.Instance;
            if (title != null && title.Showing && !(owner is QuitButton)) return false;
            switch (owner)
            {
                case MusicControlButton music:
                    return music.ButtonKind == MusicControlButton.Kind.Clear && EchoConductor.Instance != null && EchoConductor.Instance.Echoes.Count > 0;
                case TutorialReplayButton _:
                case QuitButton _:
                case StartOverButton _:
                    return true;
                case EchoBattleButton battle:
                    var director = EchoBattleDirector.Instance;
                    return battle.ButtonKind == EchoBattleButton.Kind.Leave && director != null
                        && director.State != EchoBattleDirector.Phase.Menu && director.State != EchoBattleDirector.Phase.Over
                        && director.State != EchoBattleDirector.Phase.Hidden;
                default:
                    return false;
            }
        }

        static void AskOn(Component owner)
        {
            var label = owner != null ? owner.GetComponentInChildren<LocalizedLabel>(true) : null;
            if (s_AskingLabel != null && s_AskingLabel != label) s_AskingLabel.Asking = false;
            s_AskingLabel = label;
            if (label != null) label.Asking = true;
            var card = ConfirmCard.Instance;
            if (card != null)
            {
                if (owner != null) card.Show(owner);
                else card.Hide();
            }
            if (owner != null) Debug.Log($"[RoomOrchestra] {owner.name}: asks to confirm");
        }

        /// <summary>
        /// A press landed: the thing jiggles (headset feedback, George 2026-11-03), so the player knows it registered.
        /// Furniture and the echo minis don't: furniture answers with its note, and the minis are moved by the music.
        /// Orbs keep their size (their selection is shown by size), so they wobble without the swell.
        /// </summary>
        static void Acknowledge(Component owner)
        {
            if (owner == null || owner is InstrumentTarget || owner is EchoMini) return;
            PressJiggle.Play(owner, swell: !(owner is OrbToken));
            // the menu and the confirm card play their own sounds; everything else pops (V3 review: UI sound effects)
            if (!(owner is MenuButton) && !(owner is ConfirmChoice)) UiSounds.Play(UiSound.Press, owner);
        }

        static bool Dispatch(GameObject hit)
        {
            if (hit == null)
                return false;

            // The title card: only Start does anything until the game has begun.
            var title = TitleScreen.Instance;
            if (title != null && title.Showing)
            {
                var start = hit.GetComponentInParent<TitleStartButton>();
                var quitFromTitle = hit.GetComponentInParent<QuitButton>();
                if (quitFromTitle != null)
                {
                    quitFromTitle.Press();
                    return true;
                }
                if (start == null) return false;
                start.Press();
                return true;
            }

            var quit = hit.GetComponentInParent<QuitButton>();
            if (quit != null)
            {
                quit.Press();
                return true;
            }

            var pressable = hit.GetComponentInParent<IPressable>();
            if (pressable != null)
            {
                pressable.Press();
                return true;
            }

            var spot = hit.GetComponentInParent<RealSpot>();
            if (spot != null)
            {
                RealFurniture.Instance?.Tap(spot); // the next instrument lives on this real piece
                return true;
            }

            var skip = hit.GetComponentInParent<TutorialSkipButton>();
            if (skip != null)
            {
                skip.Press();
                return true;
            }

            var change = hit.GetComponentInParent<ChangeInstrumentButton>();
            if (change != null)
            {
                change.Press();
                return true;
            }

            var battle = hit.GetComponentInParent<EchoBattleButton>();
            if (battle != null)
            {
                battle.Press();
                return true;
            }

            var view = hit.GetComponentInParent<ViewButton>();
            if (view != null)
            {
                view.Press();
                return true;
            }

            var sounds = hit.GetComponentInParent<SoundPackButton>();
            if (sounds != null)
            {
                sounds.Press();
                return true;
            }

            var lesson = hit.GetComponentInParent<TutorialReplayButton>();
            if (lesson != null)
            {
                lesson.Press();
                return true;
            }

            var selector = OrbSelector.Instance;
            var token = hit.GetComponentInParent<OrbToken>();
            if (token != null && selector != null)
            {
                selector.Select(token.Orb);
                return true;
            }

            if (hit.GetComponentInParent<TierButton>() != null && selector != null)
            {
                selector.CycleTier();
                return true;
            }

            var mini = hit.GetComponentInParent<EchoMini>();
            if (mini != null)
                return EchoConductor.Instance != null && EchoConductor.Instance.SelectMini(mini);

            var music = hit.GetComponentInParent<MusicControlButton>();
            if (music != null)
            {
                music.Press();
                return true;
            }

            var mix = hit.GetComponentInParent<MixButton>();
            if (mix != null)
            {
                mix.Press();
                return true;
            }

            var height = hit.GetComponentInParent<HeightButton>();
            if (height != null)
            {
                height.Press();
                return true;
            }

            var language = hit.GetComponentInParent<LanguageButton>();
            if (language != null)
            {
                language.Press();
                return true;
            }

            var call = hit.GetComponentInParent<TrayCallButton>();
            if (call != null)
            {
                call.Press();
                return true;
            }

            var recenter = hit.GetComponentInParent<RecenterButton>();
            if (recenter != null)
            {
                recenter.Press();
                return true;
            }

            var undo = hit.GetComponentInParent<UndoButton>();
            if (undo != null)
            {
                undo.Press();
                return true;
            }

            var tile = hit.GetComponentInParent<PitchTile>();
            if (tile != null && selector != null)
            {
                selector.PlaceOnTile(tile);
                return true;
            }

            var control = hit.GetComponentInParent<LoopControlButton>();
            if (control != null)
            {
                control.Press();
                return true;
            }

            var button = hit.GetComponentInParent<InstrumentButton>();
            if (button != null)
            {
                button.Press();
                return true;
            }

            var target = InstrumentTarget.Of(hit.transform);
            if (target != null)
            {
                // on a real object, the band pressed is the note (owner, 2026-11-08: "make it easy to tap as well so it
                // can automatically produce the note if the player doesn't want to throw it")
                if (selector != null && target.Home != null && PressPoint.HasValue) selector.PlaceOn(target, target.Home.PitchAt(PressPoint.Value));
                else if (selector != null) selector.PlaceOn(target);
                else target.PlayInstrument();
                PressPoint = null;
                return true;
            }
            return false;
        }

        /// <summary>Where the pointer touched what is being pressed (scene space), for notes along a real object.</summary>
        public static Vector3? PressPoint { get; set; }

        static void PlaceOn(InstrumentType instrument)
        {
            var target = InstrumentTarget.Find(instrument);
            if (target != null && OrbSelector.Instance != null) OrbSelector.Instance.PlaceOn(target);
        }

#if UNITY_EDITOR
        void HandleEditorInput()
        {
            var cam = editorCamera != null ? editorCamera : Camera.main;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) PlaceOn(InstrumentType.Udu);
                if (keyboard.digit2Key.wasPressedThisFrame) PlaceOn(InstrumentType.Mbira);
                if (keyboard.digit3Key.wasPressedThisFrame) PlaceOn(InstrumentType.Erhu);
                var selector = OrbSelector.Instance;
                if (selector != null)
                {
                    // Same commands a controller produces (and the same routing, incl. echo edit mode).
                    if (keyboard.rightArrowKey.wasPressedThisFrame) CommandRouter.Dispatch(ControllerCommand.NextOrb, selector);
                    if (keyboard.leftArrowKey.wasPressedThisFrame) CommandRouter.Dispatch(ControllerCommand.PreviousOrb, selector);
                    if (keyboard.upArrowKey.wasPressedThisFrame) CommandRouter.Dispatch(ControllerCommand.NextTarget, selector);
                    if (keyboard.downArrowKey.wasPressedThisFrame) CommandRouter.Dispatch(ControllerCommand.PreviousTarget, selector);
                    if (keyboard.enterKey.wasPressedThisFrame) CommandRouter.Dispatch(ControllerCommand.Place, selector);
                    if (keyboard.qKey.wasPressedThisFrame) CommandRouter.Dispatch(ControllerCommand.CycleIntensity, selector);
                    if (keyboard.pKey.wasPressedThisFrame) CommandRouter.Dispatch(ControllerCommand.CyclePetal, selector);
                    if (keyboard.eKey.wasPressedThisFrame) CommandRouter.Dispatch(ControllerCommand.ToggleEchoEdit, selector);
                    if (keyboard.rKey.wasPressedThisFrame) CommandRouter.Dispatch(ControllerCommand.Recenter, selector);
                }
                var conductor = EchoConductor.Instance;
                if (conductor != null)
                {
                    if (keyboard.cKey.wasPressedThisFrame) conductor.Clear();
                    if (keyboard.zKey.wasPressedThisFrame && (RealFurniture.Instance == null || !RealFurniture.Instance.UndoChoice())) conductor.Undo();
                    if (keyboard.kKey.wasPressedThisFrame) conductor.TogglePause();
                }
                if (keyboard.hKey.wasPressedThisFrame)
                {
                    var height = FindFirstObjectByType<HeightButton>();
                    if (height != null) height.Press();
                }
                if (keyboard.lKey.wasPressedThisFrame) Localization.Toggle();
                if (keyboard.gKey.wasPressedThisFrame && RoamingTray.Instance != null) RoamingTray.Instance.Call();
                if (keyboard.spaceKey.wasPressedThisFrame && cam != null && launcher != null)
                    launcher.ThrowAssisted(cam.transform.position + cam.transform.forward * 0.3f, cam.transform.forward, cam.transform.forward * editorThrowSpeed + Vector3.up);
            }

            var mouse = Mouse.current;
            if (mouse == null || cam == null || !mouse.leftButton.wasPressedThisFrame)
                return;
            var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out var hit, 20f)) PressPoint = hit.point;
            if (Physics.Raycast(ray, out hit, 20f) && Activate(hit.collider.gameObject))
                return;
            if (launcher != null)
                launcher.ThrowAssisted(ray.origin + ray.direction * 0.3f, ray.direction, ray.direction * editorThrowSpeed + Vector3.up);
        }
#endif
    }
}
