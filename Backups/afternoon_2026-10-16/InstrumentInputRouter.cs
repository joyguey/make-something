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
    /// tap furniture or loop buttons to activate them.
    /// Unity Editor only: click furniture/buttons, click elsewhere to throw toward the cursor,
    /// keys 1/2/3 play instruments, Space throws straight ahead, R toggles loop record, C clears.
    /// </summary>
    public class InstrumentInputRouter : MonoBehaviour
    {
        [SerializeField] BallLauncher launcher;
        [Tooltip("Editor-only preview camera used for mouse input; falls back to Camera.main.")]
        [SerializeField] Camera editorCamera;
        [SerializeField, Min(0.1f)] float editorThrowSpeed = 7f;

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
                        else
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

        /// <summary>Activates the loop button, button, or instrument that owns <paramref name="hit"/>.</summary>
        public static bool Activate(GameObject hit)
        {
            if (hit == null)
                return false;

            var slot = hit.GetComponent<PassSlotButton>();
            if (slot != null)
            {
                slot.Press();
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
                target.PlayInstrument();
                return true;
            }
            return false;
        }

#if UNITY_EDITOR
        void HandleEditorInput()
        {
            var cam = editorCamera != null ? editorCamera : Camera.main;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) InstrumentTarget.Find(InstrumentType.Udu)?.Play(InstrumentTarget.HitSource.Key);
                if (keyboard.digit2Key.wasPressedThisFrame) InstrumentTarget.Find(InstrumentType.Mbira)?.Play(InstrumentTarget.HitSource.Key);
                if (keyboard.digit3Key.wasPressedThisFrame) InstrumentTarget.Find(InstrumentType.Erhu)?.Play(InstrumentTarget.HitSource.Key);
                if (keyboard.rKey.wasPressedThisFrame) LoopStation.Instance?.ToggleRecording();
                if (keyboard.cKey.wasPressedThisFrame) LoopStation.Instance?.Clear();
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
