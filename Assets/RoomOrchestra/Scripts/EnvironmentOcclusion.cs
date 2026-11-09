using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// PICO's depth-based occlusion (owner, headset 2026-11-08: "use PICO's own depth-based occlusion"): the headset
    /// senses the depth of everything real in view (walls, furniture, hands) and <c>EnvironmentDepthOccluder</c> writes
    /// it into the depth buffer before the game draws, so whatever is behind something real is hidden. Lives on the XR
    /// rig; on the device it adds PICO's <c>PXR_EnvironmentDepthManager</c> (which starts the depth sensing and hands
    /// the depth image to shaders) and a quad just in front of the eye camera that carries the occluder.
    /// On only after the lesson's virtual room has come down and while the scanned room is not glowing (the same rule
    /// as the scanned-room shield, <see cref="RealWalls"/>), and only when PICO actually delivers depth; while it is
    /// on, the scanned-room shield steps aside. The player can turn it off (Menu: "Real things hide the game").
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public class EnvironmentOcclusion : MonoBehaviour
    {
        public const string PrefKey = "LittleEchoes.RealHides";
        /// <summary>The quad's distance in front of the eyes (m; beyond the camera's near plane) and its size.</summary>
        public const float QuadDistance = 0.2f, QuadSize = 2f;
        /// <summary>
        /// Owner, headset 2026-11-08: "the depth occlusion seems to not be very accurate ... map onto the shapes the headset
        /// recognizes as furniture". PICO's depth image is off; the scanned room mesh (<see cref="RealWalls"/>) does the
        /// hiding instead. The "Hiding" setting still turns it on and off.
        /// </summary>
        public static readonly bool UseHeadsetDepth = false;

        [SerializeField] Material occluder;

        GameObject m_Quad;
        Behaviour m_Manager;
        float m_NextLog;

        public static EnvironmentOcclusion Instance { get; private set; }
        /// <summary>The headset's depth is hiding things this frame.</summary>
        public static bool Active { get; private set; }
        /// <summary>PICO delivers depth on this device.</summary>
        public bool DepthAvailable { get; private set; }

        /// <summary>Real things hide the game's things (the player's setting, on unless turned off).</summary>
        static bool? s_Wanted;

        public static bool Wanted
        {
            get => s_Wanted ??= PlayerPrefs.GetInt(PrefKey, 1) != 0;
            set
            {
                s_Wanted = value;
                PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Debug.Log($"[RoomOrchestra] Real things hide the game: {(value ? "on" : "off")}");
            }
        }

        public void Configure(Material depthOccluder) => occluder = depthOccluder;

        /// <summary>Start over: read the setting again (its saved value was wiped).</summary>
        public static void ForgetSetting() => s_Wanted = null;

        void OnEnable() => Instance = this;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_Wanted = null;
            Active = false;
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            Active = false;
        }

        void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR && ENABLE_PICO_XR_SDK
            if (!UseHeadsetDepth)
            {
                Debug.Log("[RoomOrchestra] Environment depth: off; the scanned room mesh hides what is behind real things");
                return;
            }
            if (occluder == null) return;
            m_Manager = gameObject.AddComponent<ByteDance.PICO.XR.PXR_EnvironmentDepthManager>();
            var head = XrRig.Instance != null ? XrRig.Instance.Head : null;
            if (head == null) return;
            m_Quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            m_Quad.name = "EnvironmentDepthOccluder";
            Destroy(m_Quad.GetComponent<Collider>());
            m_Quad.transform.SetParent(head.transform, false);
            m_Quad.transform.localPosition = new Vector3(0f, 0f, QuadDistance);
            m_Quad.transform.localRotation = Quaternion.identity;
            m_Quad.transform.localScale = Vector3.one * QuadSize;
            var r = m_Quad.GetComponent<MeshRenderer>();
            r.sharedMaterial = occluder;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            m_Quad.SetActive(false);
            Debug.Log("[RoomOrchestra] Environment depth: PICO's depth sensing started for occlusion");
#endif
        }

        void Update()
        {
#if UNITY_ANDROID && !UNITY_EDITOR && ENABLE_PICO_XR_SDK
            var manager = m_Manager as ByteDance.PICO.XR.PXR_EnvironmentDepthManager;
            bool depth = manager != null && manager.IsDepthAvailable;
            if (depth != DepthAvailable)
            {
                DepthAvailable = depth;
                Debug.Log($"[RoomOrchestra] Environment depth: {(depth ? "available" : "not available")}");
            }
#endif
            bool on = m_Quad != null && DepthAvailable && Wanted && RealWalls.OcclusionWanted;
            if (m_Quad != null && m_Quad.activeSelf != on) m_Quad.SetActive(on);
            if (on != Active)
            {
                Active = on;
                Debug.Log($"[RoomOrchestra] Environment depth hides what is behind real things: {on}");
            }
            if (on && Time.time > m_NextLog)
            {
                m_NextLog = Time.time + 30f;
                Debug.Log("[RoomOrchestra] Environment depth: occluding");
            }
        }
    }
}
