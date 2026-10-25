using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Sits on the stage root. Recenter moves the whole stage so its start point is under the
    /// player's head and it faces where they look (height unchanged), so a seated player never has
    /// to walk to the table, chair or lamp. Only on request, never at startup: PICO Spatial keeps the
    /// exported pose of objects moved before its renderer has connected.
    /// </summary>
    public class StageAnchor : MonoBehaviour
    {
        public static StageAnchor Instance { get; private set; }

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
            return true;
        }

        bool TryGetHeadPose(out Pose pose)
        {
            pose = default;
#if UNITY_ANDROID && !UNITY_EDITOR
            // Full Space (Unconstrained spatial camera) reports the headset pose in scene space.
            int code = ByteDance.PICO.SpatialAdapter.SpatialAdapterRuntime.GetHMDPose(ref pose);
            bool valid = pose.rotation != new Quaternion(0f, 0f, 0f, 0f);
            if (!valid) Debug.Log($"[RoomOrchestra] GetHMDPose returned {code} with no rotation");
            return valid;
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
