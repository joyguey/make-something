using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;
using InputDevice = UnityEngine.XR.InputDevice;

namespace RoomOrchestra
{
    /// <summary>
    /// The headset rig in PICO XR mode (owner, 2026-11-08: move off PICO Spatial). Unity draws the scene itself now,
    /// so this object carries the eye camera and keeps it on the headset's pose, at floor level, turns on video
    /// seethrough (the real room behind everything) and, once the head is tracked, recentres the stage on the player
    /// so the leaf and furniture start in front of them wherever they sit.
    /// The rig stays at the scene's origin: tracking space is scene space, which is where the room scan arrives.
    /// The eye camera is saved switched off and switched on only on the device, so the Editor's preview, tests and
    /// <see cref="Camera.main"/> lookups there are unchanged. PICO's manager is added on the device only too.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class XrRig : MonoBehaviour
    {
        /// <summary>Recentre once the head has been tracked this long (the first poses can still settle).</summary>
        public const float SettleSeconds = 0.5f;
        const float RetrySeconds = 1f;

        [SerializeField] Camera head;

        InputDevice m_Hmd;
        float m_NextTry, m_TrackedSince = -1f;
        bool m_Recentred;

        public static XrRig Instance { get; private set; }
        public Camera Head => head;
        /// <summary>Has the headset reported a pose yet?</summary>
        public bool Tracked { get; private set; }

        public void Configure(Camera eyes) => head = eyes;

        void Awake()
        {
            Instance = this;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (head != null)
            {
                head.enabled = true;
                if (head.TryGetComponent<AudioListener>(out var ears)) ears.enabled = true;
            }
#if ENABLE_PICO_XR_SDK
            // after the camera is on: PICO's manager looks for Camera.main among its children when it wakes
            if (!TryGetComponent<ByteDance.PICO.XR.PXR_Manager>(out _)) gameObject.AddComponent<ByteDance.PICO.XR.PXR_Manager>();
#endif
#endif
        }

        void OnEnable() => Application.onBeforeRender += Track;

        void OnDisable()
        {
            Application.onBeforeRender -= Track;
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            var inputs = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(inputs);
            foreach (var s in inputs) s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
            Seethrough();
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused) Seethrough();
        }

        static void Seethrough()
        {
#if UNITY_ANDROID && !UNITY_EDITOR && ENABLE_PICO_XR_SDK
            ByteDance.PICO.XR.PXR_Manager.EnableVideoSeeThrough = true; // the real room is the stage
#endif
        }

        void Update()
        {
            Track();
            if (m_Recentred || !Tracked) return;
            if (m_TrackedSince < 0f) m_TrackedSince = Time.time;
            if (Time.time - m_TrackedSince < SettleSeconds) return;
            var anchor = StageAnchor.Instance;
            if (anchor == null) return;
            m_Recentred = anchor.RecenterToHead(); // the seat under the player, the stage facing where they look
            if (m_Recentred) Debug.Log("[RoomOrchestra] XR: stage recentred on the player at startup");
        }

        void Track()
        {
            if (head == null || !Application.isPlaying) return;
            if (!m_Hmd.isValid)
            {
                if (Time.unscaledTime < m_NextTry) return;
                m_NextTry = Time.unscaledTime + RetrySeconds;
                m_Hmd = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
                if (!m_Hmd.isValid) return;
            }
            bool p = m_Hmd.TryGetFeatureValue(CommonUsages.centerEyePosition, out Vector3 position);
            bool r = m_Hmd.TryGetFeatureValue(CommonUsages.centerEyeRotation, out Quaternion rotation);
            if (!p || !r || rotation == new Quaternion(0f, 0f, 0f, 0f)) return;
            head.transform.SetLocalPositionAndRotation(position, rotation);
            if (!Tracked) Debug.Log($"[RoomOrchestra] XR: head tracked at {position:F2}");
            Tracked = true;
        }

        /// <summary>The headset's pose in scene space, once tracked.</summary>
        public bool TryGetHeadPose(out Pose pose)
        {
            pose = default;
            if (!Tracked || head == null) return false;
            pose = new Pose(head.transform.position, head.transform.rotation);
            return true;
        }
    }
}
