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
        readonly List<XRInputSubsystem> m_Inputs = new();

        public static XrRig Instance { get; private set; }

        // ---- The headset's own recenter (V3 review: "Holding re-center on the headset doesn't do anything") ----
        // Holding the PICO button moves the headset's tracking origin to where the player is and faces. The rig then
        // turns and shifts by the opposite amount, so the player's view of the real room and of everything the game
        // has put in it (the scanned room, the instruments on real furniture, flowers) stays exactly where it was;
        // then the stage, the leaf and its cloud come round in front of the player, facing where they look, as the
        // Recenter button does. Room scans and saved anchors arrive in tracking space: they come into the scene
        // through the rig (TrackingToScene), so they line up before and after.
        static Matrix4x4 s_TrackingToScene = Matrix4x4.identity;
        /// <summary>From the headset's tracking space into scene space (identity until the headset is recentred).</summary>
        public static Matrix4x4 TrackingToScene => s_TrackingToScene;
        public static Vector3 ToScene(Vector3 tracking) => s_TrackingToScene.MultiplyPoint3x4(tracking);
        public static Quaternion ToScene(Quaternion tracking) => s_TrackingToScene.rotation * tracking;
        public static Vector3 FromScene(Vector3 scene) => s_TrackingToScene.inverse.MultiplyPoint3x4(scene);
        public static Quaternion FromScene(Quaternion scene) => Quaternion.Inverse(s_TrackingToScene.rotation) * scene;

        /// <summary>After the headset's recenter has been answered (tests and the log).</summary>
        public static event System.Action SystemRecentered;

        Vector3 m_LastLocalPosition;
        Quaternion m_LastLocalRotation = Quaternion.identity;
        int m_RecenterFrames = -1; // frames to wait for the new tracking pose after a recenter event
        bool m_HaveLastPose;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetTracking() => s_TrackingToScene = Matrix4x4.identity;

        void OnSystemRecenter()
        {
            if (m_RecenterFrames < 0) m_RecenterFrames = 2;
            Debug.Log("[RoomOrchestra] XR: the headset was recentred");
        }

        void OnTrackingOriginUpdated(XRInputSubsystem _) => OnSystemRecenter();

        /// <summary>
        /// The headset's tracking moved: the same physical head pose was <paramref name="before"/> (tracking space,
        /// before) and is <paramref name="after"/> now. Turns and shifts the rig so the head stays where it was in the
        /// scene, then recentres the stage on the player. Public for tests.
        /// </summary>
        public void AnswerRecenter(Pose before, Pose after)
        {
            float yawBefore = before.rotation.eulerAngles.y, yawAfter = after.rotation.eulerAngles.y;
            var worldBefore = transform.TransformPoint(before.position);
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y + Mathf.DeltaAngle(yawAfter, yawBefore), 0f);
            var shift = worldBefore - transform.TransformPoint(after.position);
            shift.y = 0f; // the floor stays the floor
            transform.position += shift;
            s_TrackingToScene = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            if (head != null) head.transform.SetLocalPositionAndRotation(after.position, after.rotation);
            Physics.SyncTransforms();
            Debug.Log($"[RoomOrchestra] XR: the real room kept in place (rig turned {Mathf.DeltaAngle(yawAfter, yawBefore):F0}°, moved {shift:F2}); the stage comes to the player");
            StageAnchor.Instance?.RecenterToHead();
            SystemRecentered?.Invoke();
        }
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
            if (!TryGetComponent<ByteDance.PICO.XR.PXR_Manager>(out var manager)) manager = gameObject.AddComponent<ByteDance.PICO.XR.PXR_Manager>();
            manager.eyeTracking = true; // look and pinch follows the eyes where the headset can (GazeSource)
#endif
#endif
        }

        void OnEnable()
        {
            Application.onBeforeRender += Track;
#if UNITY_ANDROID && !UNITY_EDITOR && ENABLE_PICO_XR_SDK
            ByteDance.PICO.XR.PXR_Plugin.System.RecenterSuccess += OnSystemRecenter;
#endif
        }

        void OnDisable()
        {
            Application.onBeforeRender -= Track;
#if UNITY_ANDROID && !UNITY_EDITOR && ENABLE_PICO_XR_SDK
            ByteDance.PICO.XR.PXR_Plugin.System.RecenterSuccess -= OnSystemRecenter;
#endif
            foreach (var s in m_Inputs) s.trackingOriginUpdated -= OnTrackingOriginUpdated;
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            SubsystemManager.GetSubsystems(m_Inputs);
            foreach (var s in m_Inputs)
            {
                s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
                s.trackingOriginUpdated += OnTrackingOriginUpdated;
            }
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
            if (m_RecenterFrames >= 0 && head != null)
            {
                // the pose before the recenter is last frame's; wait a frame or two for the new one to arrive
                if (m_RecenterFrames-- == 0)
                {
                    m_RecenterFrames = -1;
                    Track();
                    if (m_HaveLastPose)
                        AnswerRecenter(new Pose(m_LastLocalPosition, m_LastLocalRotation), new Pose(head.transform.localPosition, head.transform.localRotation));
                }
                return; // hold the last pose meanwhile (Track would overwrite it)
            }
            Track();
            if (Tracked)
            {
                m_LastLocalPosition = head.transform.localPosition;
                m_LastLocalRotation = head.transform.localRotation;
                m_HaveLastPose = true;
            }
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
