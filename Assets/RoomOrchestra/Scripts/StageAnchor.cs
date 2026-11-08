using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Sits on the stage root. Recenter moves the whole stage so its start point is under the
    /// player's head and it faces where they look (height unchanged), so a seated player never has
    /// to walk to the table, chair or lamp. On request (Recenter), and once at startup in PICO XR mode
    /// (<see cref="XrRig"/>), when the headset is first tracked.
    /// </summary>
    public class StageAnchor : MonoBehaviour
    {
        public static StageAnchor Instance { get; private set; }
        /// <summary>After a recenter (the seat is under the player again).</summary>
        public static event System.Action Recentered;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool RecenterToHead()
        {
            if (!TryGetHeadPose(out var head))
            {
                Debug.Log("[RoomOrchestra] Recenter skipped: head pose unavailable");
                return false;
            }
            return Recenter(head.position, head.rotation);
        }

        public bool Recenter(Vector3 headPosition, Quaternion headRotation)
        {
            var forward = headRotation * Vector3.forward;
            var up = headRotation * Vector3.up;
            if (!StagePlacement.TryRecenter(headPosition.x, headPosition.z, forward.x, forward.y, forward.z, up.x, up.y, up.z, out var pose))
                return false;
            transform.SetPositionAndRotation(new Vector3(pose.X, transform.position.y, pose.Z), Quaternion.Euler(0f, pose.YawDegrees, 0f));
            // Bodies (interpolated ones especially) would otherwise pull themselves back to their old pose.
            Physics.SyncTransforms();
            foreach (var body in GetComponentsInChildren<Rigidbody>())
            {
                body.position = body.transform.position;
                body.rotation = body.transform.rotation;
            }
            Debug.Log($"[RoomOrchestra] Recentered stage to ({pose.X:F2}, {pose.Z:F2}) facing {pose.YawDegrees:F0}°");
            Recentered?.Invoke();
            return true;
        }

        public bool TryGetHeadPose(out Pose pose)
        {
            pose = default;
#if UNITY_ANDROID && !UNITY_EDITOR
            // PICO XR: the rig's eye camera follows the headset in scene space.
            var rig = XrRig.Instance;
            return rig != null && rig.TryGetHeadPose(out pose);
#else
            // Editor: a camera that is not part of the stage (the preview camera moves with it).
            var cam = Camera.main;
            if (cam == null || cam.transform.IsChildOf(transform)) return false;
            pose = new Pose(cam.transform.position, cam.transform.rotation);
            return true;
#endif
        }
    }
}
