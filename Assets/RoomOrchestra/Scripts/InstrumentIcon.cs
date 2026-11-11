using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// A small 3D picture of an instrument (owner, 2026-11-11: "a small icon created depicting each instrument just in
    /// case there are names people have never heard of before"): the udu's pot, the mbira's tines, the erhu with its
    /// bow, the ocarina, the handpan, the guzheng, the balafon and the slit drum (ArtSource/Icons). It shows the
    /// instrument its furniture plays now (it follows Change), or a fixed one; it sways gently so it reads as an object.
    /// </summary>
    public class InstrumentIcon : MonoBehaviour
    {
        public const float SwayDegrees = 18f, SwaySeconds = 4f;

        [SerializeField] InstrumentTarget target;
        [SerializeField] bool hasFixed;
        [SerializeField] InstrumentType fixedType;

        MeshFilter m_Filter;
        MeshRenderer m_Renderer;
        int m_Shown = -1;
        Quaternion m_Rest;

        /// <summary>Faces this way (scene space) and sways about it; unset, it sways about its own resting turn.</summary>
        public Quaternion? Facing { get; set; }

        /// <summary>The instrument the icon shows now.</summary>
        public InstrumentType? Showing => m_Shown < 0 ? null : (InstrumentType)m_Shown;

        public void Configure(InstrumentTarget follow) => target = follow;

        public void Show(InstrumentType type)
        {
            hasFixed = true;
            fixedType = type;
            Refresh();
        }

        /// <summary>A new icon under <paramref name="parent"/> (runtime: on real objects and the choosing spot).</summary>
        public static InstrumentIcon Create(Transform parent, string name, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * size;
            go.AddComponent<MeshFilter>();
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.AddComponent<InstrumentIcon>();
        }

        void Awake()
        {
            m_Filter = GetComponent<MeshFilter>();
            m_Renderer = GetComponent<MeshRenderer>();
            m_Rest = transform.localRotation;
        }

        void OnEnable() => Refresh();

        void Refresh()
        {
            var library = InstrumentIconLibrary.Instance;
            InstrumentType? type = hasFixed ? fixedType : target != null ? target.Instrument : null;
            if (library == null || type == null || m_Filter == null) return;
            int t = (int)type.Value;
            if (t == m_Shown) return;
            m_Shown = t;
            m_Filter.sharedMesh = library.MeshFor(type.Value);
            if (m_Renderer != null && m_Renderer.sharedMaterial != library.Look) m_Renderer.sharedMaterial = library.Look;
        }

        void Update()
        {
            Refresh();
            float yaw = VineGrowth.ReducedMotion ? 0f : SwayDegrees * Mathf.Sin(Time.time * Mathf.PI * 2f / SwaySeconds);
            if (Facing.HasValue) transform.rotation = Facing.Value * Quaternion.Euler(0f, yaw, 0f);
            else transform.localRotation = m_Rest * Quaternion.Euler(0f, yaw, 0f);
        }
    }
}
