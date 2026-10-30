using System.Collections.Generic;
using ByteDance.PICO.SpatialAdapter;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace RoomOrchestra
{
    /// <summary>
    /// Single input path for the room.
    /// Device/emulator (PICO Spatial Input): pinch-drag-release the dispenser ball to throw it
    /// (a quick tap tosses it; aim assist snaps throws to the nearest instrument);
    /// tap a tray orb to select it, tap furniture to place the selected orb there, tap loop buttons.
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
        [Tooltip("PICO Spatial Input can report one tap as two touches; repeat taps on the same object " +
                 "within this window (seconds) are ignored.")]
        [SerializeField, Min(0f)] float tapDebounce = 0.2f;

        readonly HashSet<int> m_Grabbing = new();

        void OnEnable() => EnhancedTouchSupport.Enable();
        void OnDisable() => EnhancedTouchSupport.Disable();

        void Update()
        {
            foreach (var touch in Touch.activeTouches)
            {
                SpatialInputState state = SpatialInputSupport.GetInputState(touch);
                int id = state.interactionId;
                switch (state.phase)
                {
                    case TouchPhase.Began:
                        if (launcher != null && launcher.IsReadyBall(state.targetObject))
                        {
                            m_Grabbing.Add(id);
                            launcher.BeginGrab(id, state.currentPosition);
                        }
                        else if (!IsDuplicateTap(state.targetObject))
                        {
                            Activate(state.targetObject);
                        }
                        break;
                    case TouchPhase.Moved:
                        if (m_Grabbing.Contains(id))
                            launcher.UpdateGrab(id, state.currentPosition, state.currentPosition - state.devicePosition);
                        break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        if (m_Grabbing.Remove(id))
                            launcher.EndGrab(id, state.currentPosition - state.devicePosition);
                        break;
                }
            }

#if UNITY_EDITOR
            HandleEditorInput();
#endif
        }

        /// <summary>True if this tap repeats a tap on the same object within <see cref="tapDebounce"/>.</summary>
        bool IsDuplicateTap(GameObject hit)
        {
            if (hit == null) return false;
            Component owner = hit.GetComponentInParent<LoopControlButton>();
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
            if (owner == null) owner = hit.GetComponentInParent<TutorialSkipButton>();
            if (owner == null) owner = hit.GetComponentInParent<TutorialReplayButton>();
            if (owner == null) owner = hit.GetComponentInParent<EchoMini>();
            if (owner == null) owner = hit.GetComponentInParent<InstrumentTarget>();
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
            if (hit == null)
                return false;

            // The title card: only Start does anything until the game has begun.
            var title = TitleScreen.Instance;
            if (title != null && title.Showing)
            {
                var start = hit.GetComponentInParent<TitleStartButton>();
                if (start == null) return false;
                start.Press();
                return true;
            }

            var skip = hit.GetComponentInParent<TutorialSkipButton>();
            if (skip != null)
            {
                skip.Press();
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

            var target = hit.GetComponentInParent<InstrumentTarget>();
            if (target != null)
            {
                if (selector != null) selector.PlaceOn(target);
                else target.PlayInstrument();
                return true;
            }
            return false;
        }

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
                    if (keyboard.zKey.wasPressedThisFrame) conductor.Undo();
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
            if (Physics.Raycast(ray, out var hit, 20f) && Activate(hit.collider.gameObject))
                return;
            if (launcher != null)
                launcher.ThrowAssisted(ray.origin + ray.direction * 0.3f, ray.direction, ray.direction * editorThrowSpeed + Vector3.up);
        }
#endif
    }
}
