using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Holder for every tappable control (tray, buttons, pitch columns). Standing lifts it; Seated puts
    /// it back at its authored place (local origin). Never moves at startup.
    /// </summary>
    public class ControlsRig : MonoBehaviour
    {
        void OnEnable() => ComfortSettings.PostureChanged += Apply;
        void OnDisable() => ComfortSettings.PostureChanged -= Apply;

        public void Apply(PlayPosture posture)
        {
            transform.localPosition = Vector3.up * PostureLayout.ControlsLift(posture);
            Physics.SyncTransforms(); // pointer rays must hit the controls where they now are
        }
    }
}
